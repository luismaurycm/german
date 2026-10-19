using System.Text.Json;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;

namespace Acelerador.Core.Consolidacion;

/// <summary>
/// La lógica que BPC ejecuta con lo que el acelerador despliega, para el simulador: validación de cuadre (RN-16), paquete de
/// importación (RF-17), conversión (RF-26, RF-33) y eliminación intercompañía (RF-19). Lee los parámetros de la definición
/// JSON que reciben las funciones Z y usa <see cref="MotorConsolidacion"/>.
/// </summary>
public sealed class LogicaBpcReferencia : ILogicaSimulada
{
    public ResultadoOperacionSap? Ejecutar(OperacionSap operacion, IAlmacenSimulado almacen, OpcionesSimulador opciones)
    {
        using var doc = JsonDocument.Parse(operacion.Definicion);
        var d = doc.RootElement;
        return d.GetProperty("logica").GetString() switch
        {
            "VALIDAR_CUADRE" => ValidarCuadre(d, almacen),
            "IMPORTAR" => Importar(d, almacen),
            "CONVERSION" => Convertir(d, almacen),
            "ELIMINACION_IC" => EliminarIntercompania(d, almacen),
            "PARTICIPACIONES" => GuardarParticipaciones(d, almacen),
            "INVERSION" => EliminarInversion(d, almacen),
            "PARTICIPACION" => AplicarParticipacion(d, almacen),
            _ => null,
        };
    }

    /// <summary>
    /// Guardar datos de C02 en el modelo de propiedad (RF-31): reemplaza las participaciones de esas sociedades en el periodo
    /// y audita cada valor que cambia con usuario, fecha, valor anterior y nuevo (RNF-Aud-02).
    /// </summary>
    private static ResultadoOperacionSap GuardarParticipaciones(JsonElement d, IAlmacenSimulado almacen)
    {
        var grupo = d.GetProperty("grupo").GetString()!;
        var periodo = MotorConsolidacion.PeriodoBpc(d.GetProperty("periodo").GetString()!);
        var usuario = d.GetProperty("usuario").GetString()!;
        var fecha = d.GetProperty("fecha").GetDateTimeOffset();
        var nuevas = d.GetProperty("filas").EnumerateArray().Select(f => new Participacion(
            f.GetProperty("sociedad").GetString()!, f.GetProperty("control").GetDecimal(), f.GetProperty("consolidacion").GetDecimal(),
            f.GetProperty("propiedad").GetDecimal(), Enum.Parse<MetodoConsolidacion>(f.GetProperty("metodo").GetString()!))).ToList();

        var antes = ModeloPropiedad.DelPeriodo(almacen.Registros, grupo, periodo);
        if (antes.Count == 0)
        {
            antes = ModeloPropiedad.Vigentes(almacen.Registros, grupo, periodo).Participaciones;
        }

        var cambios = new List<AuditoriaBpc>();
        foreach (var n in nuevas)
        {
            var a = antes.FirstOrDefault(x => x.Sociedad == n.Sociedad);
            void Cambio(string dato, string? anterior, string nuevo)
            {
                if (anterior != nuevo)
                {
                    cambios.Add(new AuditoriaBpc(fecha, usuario, ModeloPropiedad.Modelo, n.Sociedad, periodo, dato, anterior ?? "—", nuevo));
                }
            }

            Cambio(ModeloPropiedad.Control, a?.Control.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), n.Control.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            Cambio(ModeloPropiedad.Consolidacion, a?.Consolidacion.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), n.Consolidacion.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            Cambio(ModeloPropiedad.Propiedad, a?.Propiedad.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), n.Propiedad.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture));
            Cambio(ModeloPropiedad.Metodo, a?.Metodo.ToString(), n.Metodo.ToString());
        }

        var sociedades = nuevas.Select(n => n.Sociedad).ToHashSet();
        almacen.Reemplazar(r => r.Modelo == ModeloPropiedad.Modelo && r.Grupo == grupo && r.Periodo == periodo && sociedades.Contains(r.Entidad),
            ModeloPropiedad.Registros(grupo, periodo, nuevas));
        almacen.Auditar(cambios);
        return new ResultadoOperacionSap(true, $"Participaciones de {nuevas.Count} sociedades guardadas en {periodo}; {cambios.Count} cambios auditados.",
            ResultadoEscritura.Creado, Cantidad: nuevas.Count);
    }

    private static (string Grupo, string Periodo, IReadOnlyList<Participacion> Vigentes, IReadOnlyDictionary<string, ClaseCuenta> Clases, IReadOnlyList<RegistroBpc> Base) Contexto(JsonElement d, IAlmacenSimulado almacen)
    {
        var grupo = d.GetProperty("grupo").GetString()!;
        var periodo = MotorConsolidacion.PeriodoBpc(d.GetProperty("periodo").GetString()!);
        var vigentes = ModeloPropiedad.Vigentes(almacen.Registros, grupo, periodo).Participaciones;
        var clases = d.GetProperty("clases").EnumerateObject().ToDictionary(p => p.Name, p => Enum.Parse<ClaseCuenta>(p.Value.GetString()!));
        var base_ = almacen.Registros.Where(r => r.Modelo == MotorConsolidacion.Modelo && r.Periodo == periodo && r.Moneda != MotorConsolidacion.MonedaLocal && r.Grupo.Length == 0).ToList();
        return (grupo, periodo, vigentes, clases, base_);
    }

    private static ResultadoOperacionSap EliminarInversion(JsonElement d, IAlmacenSimulado almacen)
    {
        var (grupo, periodo, vigentes, clases, base_) = Contexto(d, almacen);
        if (vigentes.Count == 0)
        {
            return new ResultadoOperacionSap(false, $"No hay participaciones en el modelo de propiedad para {grupo} en {periodo} (C02).");
        }

        var inversiones = d.GetProperty("cuentasInversion").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
        var registros = MotorConsolidacion.EliminarInversion(base_, vigentes, d.GetProperty("matriz").GetString()!, inversiones, clases, grupo);
        almacen.Reemplazar(r => r.Periodo == periodo && r.Grupo == grupo && r.Origen == MotorConsolidacion.OrigenEliminacionInversion, registros);
        var subsidiarias = vigentes.Where(p => p.Metodo == MetodoConsolidacion.IntegracionGlobal).Select(p => $"{p.Sociedad} ({p.Propiedad:0.##} %)");
        var im = registros.Where(r => r.Cuenta == ReglasRubros.CuentaInteresMinoritario.Id).GroupBy(r => r.Moneda).Select(g => $"{g.Key} {ReglaCuadre.Formato(g.Sum(r => r.Importe))}");
        return new ResultadoOperacionSap(true,
            $"Inversión eliminada en {string.Join(", ", subsidiarias)}; interés minoritario en {ReglasRubros.CuentaInteresMinoritario.Id}: {string.Join(" · ", im)}.",
            ResultadoEscritura.Creado, Cantidad: registros.Count);
    }

    private static ResultadoOperacionSap AplicarParticipacion(JsonElement d, IAlmacenSimulado almacen)
    {
        var (grupo, periodo, vigentes, clases, base_) = Contexto(d, almacen);
        var registros = MotorConsolidacion.AplicarParticipacion(base_, vigentes, d.GetProperty("matriz").GetString()!, d.GetProperty("cuentaInversion").GetString()!, clases, grupo);
        almacen.Reemplazar(r => r.Periodo == periodo && r.Grupo == grupo && r.Origen == MotorConsolidacion.OrigenParticipacion, registros);
        var coligadas = vigentes.Where(p => p.Metodo == MetodoConsolidacion.Participacion).Select(p => $"{p.Sociedad} ({p.Propiedad:0.##} %)").ToList();
        return new ResultadoOperacionSap(true,
            coligadas.Count == 0 ? "No hay coligadas con método participación en el periodo." : $"Participación en el resultado reconocida de {string.Join(", ", coligadas)}.",
            ResultadoEscritura.Creado, Cantidad: registros.Count);
    }

    private static IReadOnlyList<SaldoErp> Datos(JsonElement d, IAlmacenSimulado almacen)
    {
        var sociedad = d.GetProperty("sociedad").GetString()!;
        return d.GetProperty("enSap").GetBoolean() ? almacen.SaldosErp(sociedad) : almacen.DatosNoSap(sociedad, d.GetProperty("periodo").GetString()!);
    }

    private static ResultadoOperacionSap ValidarCuadre(JsonElement d, IAlmacenSimulado almacen)
    {
        var sociedad = d.GetProperty("sociedad").GetString()!;
        var periodo = d.GetProperty("periodo").GetString()!;
        var tolerancia = d.GetProperty("tolerancia").GetDecimal();
        var mapeo = d.GetProperty("mapeo").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        var v = ReglaCuadre.Validar(sociedad, periodo, Datos(d, almacen), mapeo, tolerancia);
        almacen.GuardarValidacion(v);
        return v.Importable
            ? new ResultadoOperacionSap(true, $"{sociedad} cuadra en {periodo} (diferencia de balance {ReglaCuadre.Formato(v.DiferenciaBalance)}).", ResultadoEscritura.Creado, Cantidad: 1)
            : new ResultadoOperacionSap(true, $"{sociedad} no cuadra en {periodo}.", ResultadoEscritura.Creado, Cantidad: 0, Aviso: ReglaCuadre.Motivo(v, tolerancia));
    }

    private static ResultadoOperacionSap Importar(JsonElement d, IAlmacenSimulado almacen)
    {
        var sociedad = d.GetProperty("sociedad").GetString()!;
        var periodo = d.GetProperty("periodo").GetString()!;
        var validacion = almacen.Validaciones.FirstOrDefault(v => v.Sociedad == sociedad && v.Periodo == periodo);
        if (validacion is not { Importable: true })
        {
            // HU-27 CA3: solo se importan las sociedades que pasan la validación de cuadre.
            return new ResultadoOperacionSap(true, $"{sociedad}: 0 registros importados.", ResultadoEscritura.Creado, Cantidad: 0,
                Aviso: $"{sociedad} no se importa en {periodo}: no pasó la validación de cuadre (ver C03).");
        }

        var grupo = d.GetProperty("grupo").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
        var registros = MotorConsolidacion.Importar(sociedad, d.GetProperty("plan").GetString()!, periodo, Datos(d, almacen), grupo);
        var periodoBpc = MotorConsolidacion.PeriodoBpc(periodo);
        almacen.Reemplazar(r => r.Entidad == sociedad && r.Periodo == periodoBpc && r.Moneda == MotorConsolidacion.MonedaLocal && r.Origen == MotorConsolidacion.OrigenCarga, registros);
        almacen.GuardarValidacion(validacion with { Importada = true });
        return new ResultadoOperacionSap(true, $"{sociedad}: {registros.Count} registros importados en {periodo}.", ResultadoEscritura.Creado, Cantidad: registros.Count);
    }

    private static ResultadoOperacionSap Convertir(JsonElement d, IAlmacenSimulado almacen)
    {
        var periodo = d.GetProperty("periodo").GetString()!;
        var periodoBpc = MotorConsolidacion.PeriodoBpc(periodo);
        var monedas = d.GetProperty("monedas").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        var presentacion = d.GetProperty("presentacion").EnumerateArray().Select(x => x.GetString()!).ToList();
        var clases = d.GetProperty("clases").EnumerateObject().ToDictionary(p => p.Name, p => Enum.Parse<ClaseCuenta>(p.Value.GetString()!));
        decimal? Tasa(string moneda, string tipo) => almacen.Tasa(moneda, tipo, periodo);

        var nuevos = new List<RegistroBpc>();
        var cta = new List<string>();
        foreach (var (sociedad, moneda) in monedas)
        {
            var locales = almacen.Registros.Where(r => r.Entidad == sociedad && r.Periodo == periodoBpc && r.Moneda == MotorConsolidacion.MonedaLocal).ToList();
            foreach (var destino in presentacion)
            {
                var (convertidos, falta) = MotorConsolidacion.Convertir(locales, moneda, destino, clases, Tasa);
                if (falta is not null)
                {
                    // HU-30 CA4: si falta una tasa del periodo el paso falla e indica la moneda.
                    return new ResultadoOperacionSap(false, $"CURR_CONVERSION: falta la tasa {falta} para {periodo}; no se convierte {sociedad}.");
                }

                nuevos.AddRange(convertidos);
                var diferencia = convertidos.Where(r => r.Cuenta == ReglasRubros.CuentaDiferenciaConversion.Id).Sum(r => r.Importe);
                if (diferencia != 0m)
                {
                    cta.Add($"{sociedad} {destino} {ReglaCuadre.Formato(diferencia)}");
                }
            }
        }

        almacen.Reemplazar(r => r.Periodo == periodoBpc && presentacion.Contains(r.Moneda) && monedas.ContainsKey(r.Entidad) && r.Grupo.Length == 0, nuevos);
        return new ResultadoOperacionSap(true,
            $"{nuevos.Count} registros convertidos a {string.Join(" y ", presentacion)}; diferencia en conversión en {ReglasRubros.CuentaDiferenciaConversion.Id}: {(cta.Count == 0 ? "sin diferencia" : string.Join(" · ", cta))}.",
            ResultadoEscritura.Creado, Cantidad: nuevos.Count);
    }

    private static ResultadoOperacionSap EliminarIntercompania(JsonElement d, IAlmacenSimulado almacen)
    {
        var periodoBpc = MotorConsolidacion.PeriodoBpc(d.GetProperty("periodo").GetString()!);
        var grupo = d.GetProperty("grupo").GetString()!;
        // RN-13 y RN-14: los métodos vigentes del periodo en el modelo de propiedad; sin modelo, los de la estructura inicial.
        var (_, vigentes) = ModeloPropiedad.Vigentes(almacen.Registros, grupo, periodoBpc);
        var metodos = vigentes.Count > 0
            ? vigentes.ToDictionary(p => p.Sociedad, p => p.Metodo)
            : d.GetProperty("metodos").EnumerateObject().ToDictionary(p => p.Name, p => Enum.Parse<MetodoConsolidacion>(p.Value.GetString()!));
        var presentacion = d.GetProperty("presentacion").EnumerateArray().Select(x => x.GetString()!).ToList();
        var base_ = almacen.Registros.Where(r => r.Modelo == MotorConsolidacion.Modelo && r.Periodo == periodoBpc && presentacion.Contains(r.Moneda) && r.Grupo.Length == 0).ToList();
        var excluir = d.TryGetProperty("excluir", out var x) ? x.EnumerateArray().Select(e => e.GetString()!).ToHashSet() : null;
        var eliminaciones = MotorConsolidacion.EliminarIntercompania(base_, metodos, grupo, excluir);
        almacen.Reemplazar(r => r.Periodo == periodoBpc && r.Grupo == grupo && r.Origen == MotorConsolidacion.OrigenEliminacionIc, eliminaciones);
        var parejas = eliminaciones.Where(r => r.Cuenta != ReglasRubros.CuentaDiferenciaIntercompania.Id).Select(r => r.Entidad + "–" + r.Intco[2..]).Distinct().Count();
        return new ResultadoOperacionSap(true, $"Eliminaciones intercompañía de {grupo}: {eliminaciones.Count} registros en {parejas} partidas entre sociedades del grupo.",
            ResultadoEscritura.Creado, Cantidad: eliminaciones.Count);
    }
}
