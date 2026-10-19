using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Servicios;

/// <summary>Una fila de C02: la sociedad, sus valores del periodo (o null), los del periodo anterior y qué cambió.</summary>
public sealed record FilaC02(Sociedad Sociedad, Participacion? Actual, Participacion? Anterior, IReadOnlyList<string> Cambios);

/// <summary>Todo lo que muestra UX-13 para un grupo y periodo.</summary>
public sealed record VistaC02(
    Cliente Cliente,
    string Grupo,
    string Periodo,
    IReadOnlyList<string> Periodos,
    IReadOnlyList<FilaC02> Filas,
    IReadOnlyList<AuditoriaBpc> Auditoria,
    string? Error)
{
    public bool Vacio => Error is null && Filas.All(f => f.Actual is null);
}

public sealed record ResultadoC02(int Guardadas, IReadOnlyList<ErrorParticipacion> Errores, string? Error);

/// <summary>
/// HU-35 (UX-13): mantener por periodo las participaciones y métodos en el modelo de propiedad de BPC, como lo hace la
/// plantilla C02 con EPM. Un cambio aplica desde el periodo elegido (RN-14) y queda en la auditoría de BPC (RNF-Aud-02).
/// </summary>
public sealed class ServicioParticipaciones(
    IClienteRepository clientes,
    ISociedadRepository sociedades,
    AccesoSap acceso,
    ISapGateway sap,
    IRegistroEjecuciones registro,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string PasoGuardar = "Guardar participaciones (C02)";

    public static string MensajeVacio => "Sin participaciones para este periodo: copia las del periodo anterior o ingrésalas.";

    /// <summary>Periodos de C02: los doce meses del año del periodo inicial.</summary>
    public static IReadOnlyList<string> Periodos(DateTimeOffset fecha)
    {
        var anio = ReglasCierre.PeriodoInicial(fecha)[..4];
        return Enumerable.Range(1, 12).Select(m => $"{anio}-{m:00}").ToList();
    }

    public async Task<VistaC02?> ObtenerAsync(Guid clienteId, string? periodo, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is null)
        {
            return null;
        }

        var lista = await sociedades.ListarAsync(clienteId, ct);
        var grupo = lista.Count == 0 ? "" : ReglasCierre.Grupo(lista);
        var elegido = periodo ?? ReglasCierre.PeriodoInicial(reloj.GetUtcNow());
        var periodos = Periodos(reloj.GetUtcNow());
        var (registros, auditoria, error) = await LeerAsync(clienteId, ct);
        var periodoBpc = MotorConsolidacion.PeriodoBpc(elegido);
        var actuales = ModeloPropiedad.DelPeriodo(registros, grupo, periodoBpc);
        var anteriores = ModeloPropiedad.Vigentes(registros, grupo, ModeloPropiedad.PeriodoAnterior(periodoBpc)).Participaciones;
        var filas = lista.Select(s =>
        {
            var actual = actuales.FirstOrDefault(p => p.Sociedad == s.Codigo);
            var anterior = anteriores.FirstOrDefault(p => p.Sociedad == s.Codigo);
            return new FilaC02(s, actual, anterior, actual is null ? [] : ReglasParticipaciones.Cambios(anterior, actual));
        }).ToList();
        return new VistaC02(cliente, grupo, elegido, periodos, filas, auditoria.OrderByDescending(a => a.Fecha).ToList(), error);
    }

    /// <summary>HU-35 CA2: las participaciones vigentes al cierre del mes anterior, para copiarlas al periodo.</summary>
    public async Task<IReadOnlyList<Participacion>> CopiarAnteriorAsync(Guid clienteId, string periodo, CancellationToken ct = default)
    {
        var lista = await sociedades.ListarAsync(clienteId, ct);
        var (registros, _, _) = await LeerAsync(clienteId, ct);
        return ModeloPropiedad.Vigentes(registros, ReglasCierre.Grupo(lista), ModeloPropiedad.PeriodoAnterior(MotorConsolidacion.PeriodoBpc(periodo))).Participaciones;
    }

    /// <summary>«Guardar datos» (HU-35 CA1, CA3, CA4, CA5): guarda las filas válidas del periodo; las que tienen error no.</summary>
    public async Task<ResultadoC02> GuardarAsync(Guid clienteId, string periodo, IReadOnlyList<FilaParticipacion> filas, CancellationToken ct = default)
    {
        var lista = await sociedades.ListarAsync(clienteId, ct);
        var (validas, errores) = ReglasParticipaciones.Validar(filas.Where(f => lista.Any(s => s.Codigo == f.Sociedad)));
        if (validas.Count == 0)
        {
            return new ResultadoC02(0, errores, null);
        }

        var destino = await acceso.DestinoAsync(clienteId, SistemaSap.Bpc, ct);
        if (destino.Destino is null)
        {
            return new ResultadoC02(0, errores, destino.Error);
        }

        var definicion = Definicion.Json(new
        {
            logica = "PARTICIPACIONES",
            grupo = ReglasCierre.Grupo(lista),
            periodo,
            usuario = usuario.Nombre,
            fecha = reloj.GetUtcNow(),
            filas = validas.Select(p => new { sociedad = p.Sociedad, control = p.Control, consolidacion = p.Consolidacion, propiedad = p.Propiedad, metodo = p.Metodo.ToString() }),
        });
        ResultadoOperacionSap r;
        try
        {
            r = await sap.EjecutarAsync(destino.Destino, new OperacionSap("Z_ACC_BPC_GUARDAR_PARTICIPACIONES", "PARTICIPACIONES", $"C02 {periodo}", definicion), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            r = new ResultadoOperacionSap(false, $"Error inesperado al llamar a SAP: {ex.Message}");
        }

        var mensaje = SanitizadorMensajes.Limpiar(r.Mensaje, destino.Credencial);
        await registro.RegistrarAsync(new Ejecucion(reloj.GetUtcNow(), usuario.Nombre, clienteId, Textos.Sistema(SistemaSap.Bpc), PasoGuardar,
            $"{ModeloPropiedad.Modelo} {periodo}: {string.Join(", ", validas.Select(v => v.Sociedad))}", r.Exitosa ? ResultadoEjecucion.Exitoso : ResultadoEjecucion.Fallido, mensaje), ct);
        return r.Exitosa ? new ResultadoC02(validas.Count, errores, null) : new ResultadoC02(0, errores, mensaje);
    }

    private async Task<(IReadOnlyList<RegistroBpc> Registros, IReadOnlyList<AuditoriaBpc> Auditoria, string? Error)> LeerAsync(Guid clienteId, CancellationToken ct)
    {
        var (datos, error) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(ModeloPropiedad.Modelo), ct);
        return (datos?.Registros ?? [], datos?.Auditoria ?? [], error);
    }
}
