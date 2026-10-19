using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Core.Reportes;
using Acelerador.Sap;

namespace Acelerador.Core.Servicios;

/// <summary>Una fila de P05: cuenta, centro y los doce meses de la versión en presentación (ingresos y gastos positivos).</summary>
public sealed record FilaPresupuesto(string Cuenta, string Descripcion, string Rubro, string Centro, IReadOnlyList<decimal> Meses)
{
    public decimal Total => Meses.Sum();
}

public sealed record VistaPresupuesto(
    ContextoCierre Contexto,
    string Sociedad,
    VersionPresupuesto Version,
    int Anio,
    IReadOnlyList<VersionPresupuesto> Versiones,
    IReadOnlyList<int> Anios,
    IReadOnlyList<FilaPresupuesto> Filas,
    bool SinPresupuesto,
    string? Error);

/// <summary>Una fila de P05 tal como se envía: cuenta, centro y doce celdas de texto.</summary>
public sealed record FilaPresupuestoTexto(string Cuenta, string Centro, IReadOnlyList<string?> Meses);

/// <summary>Una celda de P05 que no se guardó: fila (desde 1), mes (1 a 12) y motivo.</summary>
public sealed record ErrorCeldaPresupuesto(int Fila, int Mes, string Motivo);

public sealed record ResultadoPresupuesto(int Guardadas, IReadOnlyList<ErrorCeldaPresupuesto> Errores, string? Error);

/// <summary>
/// HU-50 y HU-51 (UX-12, P05): captura del presupuesto por cuenta, centro y mes en la versión y el año elegidos, sobre el
/// modelo de planeación de BPC (RN-08). Solo números; una celda inválida no se guarda; las demás versiones no cambian.
/// </summary>
public sealed class ServicioPresupuesto(
    ContextoCierreLector contexto,
    AccesoSap acceso,
    ISapGateway sap,
    IRegistroEjecuciones registro,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string PasoGuardar = "Guardar presupuesto (P05)";
    public const string SinCentro = "SIN_CENTRO";
    public const string MensajeVacio = "Sin presupuesto para esta versión: ingresa los valores.";
    public const string SoloNumeros = "Solo se aceptan números";

    public static int AnioInicial(DateTimeOffset fecha) => int.Parse(ReglasCierre.PeriodoInicial(fecha)[..4], System.Globalization.CultureInfo.InvariantCulture);

    public static IReadOnlyList<VersionPresupuesto> Versiones(Cliente c) => c.Versiones.Count == 0 ? VersionPresupuesto.Propuestas : [.. c.Versiones];

    public async Task<VistaPresupuesto?> ObtenerAsync(Guid clienteId, string? sociedad, string? version, int? anio, CancellationToken ct = default)
    {
        var c = await contexto.LeerAsync(clienteId, null, ct);
        if (c is null)
        {
            return null;
        }

        var versiones = Versiones(c.Cliente);
        var v = versiones.FirstOrDefault(x => x.Codigo == version) ?? versiones.FirstOrDefault(x => x.Anual) ?? versiones[0];
        var inicial = AnioInicial(reloj.GetUtcNow());
        var anios = new[] { inicial - 1, inicial, inicial + 1 };
        var a = anio is { } x2 && anios.Contains(x2) ? x2 : inicial;
        var soc = c.Sociedades.FirstOrDefault(s => s.Codigo == sociedad) ?? (c.Sociedades.Count == 0 ? null : ReglasCierre.Matriz(c.Sociedades));
        if (soc is null)
        {
            return new VistaPresupuesto(c, "", v, a, versiones, anios, [], true, null);
        }

        var (datos, error) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(MotorPresupuesto.Modelo, Entidad: soc.Codigo), ct);
        if (datos is null)
        {
            return new VistaPresupuesto(c, soc.Codigo, v, a, versiones, anios, [], true, error);
        }

        var asignaciones = await contexto.AsignacionesAsync(clienteId, c.Sociedades, ct);
        var rubroDe = asignaciones.GroupBy(x => GeneradorModeloBpc.IdCuenta(x.PlanCuentas, x.Cuenta)).ToDictionary(g => g.Key, g => g.First().Rubro);
        var delAnio = datos.Registros.Where(r => r.Periodo.StartsWith($"{a}.", StringComparison.Ordinal) && rubroDe.TryGetValue(r.Cuenta, out var rb) && ReglasRubros.EsResultados(ReglasRubros.Clase(rb))).ToList();
        var claves = delAnio.Select(r => (r.Cuenta, r.Centro)).Distinct().ToList();
        if (claves.Count == 0)
        {
            claves = asignaciones.Where(x => x.PlanCuentas == soc.PlanCuentas && ReglasRubros.EsResultados(ReglasRubros.Clase(x.Rubro)))
                .Select(x => (GeneradorModeloBpc.IdCuenta(x.PlanCuentas, x.Cuenta), SinCentro)).ToList();
        }

        var delaVersion = delAnio.Where(r => r.Categoria == v.Codigo).ToList();
        var filas = claves
            .OrderBy(k => rubroDe.GetValueOrDefault(k.Item1), StringComparer.Ordinal).ThenBy(k => k.Item1, StringComparer.Ordinal).ThenBy(k => k.Item2, StringComparer.Ordinal)
            .Select(k =>
            {
                var rubro = rubroDe.GetValueOrDefault(k.Item1) ?? "";
                var signo = MotorReportes.Signo(rubro);
                var meses = Enumerable.Range(1, 12).Select(m => signo * delaVersion.Where(r => r.Cuenta == k.Item1 && r.Centro == k.Item2 && MotorPresupuesto.Mes(r.Periodo) == m).Sum(r => r.Importe)).ToList();
                return new FilaPresupuesto(k.Item1, Descripcion(k.Item1), rubro, k.Item2, meses);
            }).ToList();
        return new VistaPresupuesto(c, soc.Codigo, v, a, versiones, anios, filas, delaVersion.Count == 0, null);
    }

    /// <summary>«Guardar datos» (HU-50 CA2 y CA3): guarda las celdas válidas de la versión y el año; las inválidas se informan y no se guardan.</summary>
    public async Task<ResultadoPresupuesto> GuardarAsync(Guid clienteId, string sociedad, string version, int anio, IReadOnlyList<FilaPresupuestoTexto> filas, CancellationToken ct = default)
    {
        var v = await ObtenerAsync(clienteId, sociedad, version, anio, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        if (v.Error is not null)
        {
            return new ResultadoPresupuesto(0, [], v.Error);
        }

        var rubroDe = v.Filas.ToDictionary(f => (f.Cuenta, f.Centro), f => f.Rubro);
        var errores = new List<ErrorCeldaPresupuesto>();
        var celdas = new List<object>();
        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i];
            if (!rubroDe.TryGetValue((f.Cuenta, f.Centro), out var rubro))
            {
                continue;
            }

            for (var m = 1; m <= 12; m++)
            {
                var texto = m <= f.Meses.Count ? f.Meses[m - 1] : null;
                if (!ReglasAsientos.TryLeerImporte(texto?.Replace('−', '-').TrimStart('-'), out var valor))
                {
                    errores.Add(new ErrorCeldaPresupuesto(i + 1, m, SoloNumeros));
                    continue;
                }

                var negativo = (texto ?? "").Trim().StartsWith('-') || (texto ?? "").Trim().StartsWith('−');
                celdas.Add(new { cuenta = f.Cuenta, centro = f.Centro, mes = m, importe = MotorReportes.Signo(rubro) * (negativo ? -valor : valor) });
            }
        }

        if (celdas.Count == 0)
        {
            return new ResultadoPresupuesto(0, errores, null);
        }

        var destino = await acceso.DestinoAsync(clienteId, SistemaSap.Bpc, ct);
        ResultadoOperacionSap r;
        if (destino.Destino is null)
        {
            r = new ResultadoOperacionSap(false, destino.Error!);
        }
        else
        {
            var definicion = Definicion.Json(new { logica = "PRESUPUESTO_GUARDAR", modelo = MotorPresupuesto.Modelo, sociedad = v.Sociedad, version = v.Version.Codigo, anio = v.Anio, usuario = usuario.Nombre, fecha = reloj.GetUtcNow(), celdas });
            try
            {
                r = await sap.EjecutarAsync(destino.Destino, new OperacionSap("Z_ACC_BPC_GUARDAR_PRESUPUESTO", "PRESUPUESTO", $"P05 {v.Sociedad} {v.Version.Codigo} {v.Anio}", definicion), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                r = new ResultadoOperacionSap(false, $"Error inesperado al llamar a SAP: {ex.Message}");
            }
        }

        var mensaje = SanitizadorMensajes.Limpiar(r.Mensaje, destino.Credencial);
        await registro.RegistrarAsync(new Ejecucion(reloj.GetUtcNow(), usuario.Nombre, clienteId, Textos.Sistema(SistemaSap.Bpc), PasoGuardar,
            $"{MotorPresupuesto.Modelo} {v.Sociedad} {v.Version.Codigo} {v.Anio}", r.Exitosa ? ResultadoEjecucion.Exitoso : ResultadoEjecucion.Fallido, mensaje), ct);
        return r.Exitosa ? new ResultadoPresupuesto(celdas.Count, errores, null) : new ResultadoPresupuesto(0, errores, mensaje);
    }

    private static string Descripcion(string cuenta)
    {
        var i = cuenta.IndexOf('_', StringComparison.Ordinal);
        return i > 0 ? $"Cuenta {cuenta[(i + 1)..]} del plan {cuenta[..i]}" : cuenta;
    }
}
