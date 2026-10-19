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
            "ASIENTO" => Contabilizar(d, almacen),
            "BLOQUEO" => CambiarEstado(d, almacen),
            "PRESUPUESTO_CARGA" => CargarPresupuesto(d, almacen),
            "PRESUPUESTO_GUARDAR" => GuardarPresupuesto(d, almacen),
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
        almacen.Reemplazar(r => r.Modelo == MotorConsolidacion.Modelo && r.Periodo == periodo && r.Grupo == grupo && r.Origen == MotorConsolidacion.OrigenEliminacionInversion, registros);
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
        almacen.Reemplazar(r => r.Modelo == MotorConsolidacion.Modelo && r.Periodo == periodo && r.Grupo == grupo && r.Origen == MotorConsolidacion.OrigenParticipacion, registros);
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

    /// <summary>RN-17: lo que BPC responde cuando el estado de trabajo de la sociedad no admite reprocesos.</summary>
    private static ResultadoOperacionSap? Bloqueada(IAlmacenSimulado almacen, string sociedad, string periodo, string accion) =>
        almacen.Bloqueada(sociedad, periodo)
            ? new ResultadoOperacionSap(true, $"{sociedad}: bloqueada en {periodo}.", ResultadoEscritura.Existente, Cantidad: 0,
                Aviso: $"{sociedad} está bloqueada en {periodo}: no se {accion}; se conservan sus datos (RN-17).")
            : null;

    private static ResultadoOperacionSap ValidarCuadre(JsonElement d, IAlmacenSimulado almacen)
    {
        var sociedad = d.GetProperty("sociedad").GetString()!;
        var periodo = d.GetProperty("periodo").GetString()!;
        if (Bloqueada(almacen, sociedad, periodo, "vuelve a validar") is { } bloqueada)
        {
            return bloqueada;
        }

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
        if (Bloqueada(almacen, sociedad, periodo, "reimporta") is { } bloqueada)
        {
            return bloqueada;
        }

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
        almacen.Reemplazar(r => r.Modelo == MotorConsolidacion.Modelo && r.Entidad == sociedad && r.Periodo == periodoBpc && r.Moneda == MotorConsolidacion.MonedaLocal && r.Origen == MotorConsolidacion.OrigenCarga, registros);
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
        // RN-17: una sociedad bloqueada en el periodo conserva su conversión.
        var bloqueadas = monedas.Keys.Where(s => almacen.Bloqueada(s, periodo)).ToHashSet();
        foreach (var (sociedad, moneda) in monedas.Where(m => !bloqueadas.Contains(m.Key)))
        {
            var locales = almacen.Registros.Where(r => r.Modelo == MotorConsolidacion.Modelo && r.Entidad == sociedad && r.Periodo == periodoBpc && r.Moneda == MotorConsolidacion.MonedaLocal).ToList();
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

        almacen.Reemplazar(r => r.Modelo == MotorConsolidacion.Modelo && r.Periodo == periodoBpc && presentacion.Contains(r.Moneda) && monedas.ContainsKey(r.Entidad) && !bloqueadas.Contains(r.Entidad) && r.Grupo.Length == 0, nuevos);
        return new ResultadoOperacionSap(true,
            $"{nuevos.Count} registros convertidos a {string.Join(" y ", presentacion)}; diferencia en conversión en {ReglasRubros.CuentaDiferenciaConversion.Id}: {(cta.Count == 0 ? "sin diferencia" : string.Join(" · ", cta))}.",
            ResultadoEscritura.Creado, Cantidad: nuevos.Count,
            Aviso: bloqueadas.Count == 0 ? null : $"{string.Join(", ", bloqueadas.Order())} {(bloqueadas.Count == 1 ? "está bloqueada" : "están bloqueadas")} en {periodo}: no se vuelve a convertir; se conservan sus datos (RN-17).");
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
        almacen.Reemplazar(r => r.Modelo == MotorConsolidacion.Modelo && r.Periodo == periodoBpc && r.Grupo == grupo && r.Origen == MotorConsolidacion.OrigenEliminacionIc, eliminaciones);
        var parejas = eliminaciones.Where(r => r.Cuenta != ReglasRubros.CuentaDiferenciaIntercompania.Id).Select(r => r.Entidad + "–" + r.Intco[2..]).Distinct().Count();
        return new ResultadoOperacionSap(true, $"Eliminaciones intercompañía de {grupo}: {eliminaciones.Count} registros en {parejas} partidas entre sociedades del grupo.",
            ResultadoEscritura.Creado, Cantidad: eliminaciones.Count);
    }

    /// <summary>
    /// Contabiliza un asiento de ajuste (RF-35) como lo hace BPC: rechaza el asiento que no cuadra (RN-15) o con líneas de una
    /// sociedad bloqueada (RN-17); si no, le da número y escribe sus líneas en el modelo de consolidación a nivel de grupo.
    /// </summary>
    private static ResultadoOperacionSap Contabilizar(JsonElement d, IAlmacenSimulado almacen)
    {
        var grupo = d.GetProperty("grupo").GetString()!;
        var periodo = d.GetProperty("periodo").GetString()!;
        var moneda = d.GetProperty("moneda").GetString()!;
        var lineas = d.GetProperty("lineas").EnumerateArray().Select(l => new LineaAsiento(
            l.GetProperty("sociedad").GetString()!, l.GetProperty("cuenta").GetString()!, l.GetProperty("intco").GetString()!,
            l.GetProperty("debito").GetDecimal(), l.GetProperty("credito").GetDecimal(), l.GetProperty("observacion").GetString() ?? "")).ToList();
        var diferencia = lineas.Sum(l => l.Debito) - lineas.Sum(l => l.Credito);
        if (lineas.Count == 0 || diferencia != 0m)
        {
            return new ResultadoOperacionSap(false, $"UJJ: el asiento no cuadra (diferencia {ReglaCuadre.Formato(diferencia)}).");
        }

        var bloqueadas = lineas.Select(l => l.Sociedad).Distinct().Where(s => almacen.Bloqueada(s, periodo)).ToList();
        if (bloqueadas.Count > 0)
        {
            return new ResultadoOperacionSap(false, $"UJJ: {string.Join(", ", bloqueadas)} está bloqueada en {periodo} (estado de trabajo); el asiento no se contabiliza.");
        }

        var periodoBpc = MotorConsolidacion.PeriodoBpc(periodo);
        var asiento = almacen.Contabilizar(numero => new AsientoBpc(numero, grupo, periodo, moneda, d.GetProperty("descripcion").GetString() ?? "",
            d.GetProperty("usuario").GetString()!, d.GetProperty("fecha").GetDateTimeOffset(), lineas));
        almacen.Reemplazar(_ => false, lineas.Select(l => new RegistroBpc(MotorConsolidacion.Modelo, l.Sociedad, l.Cuenta, l.Intco, periodoBpc, moneda,
            MotorConsolidacion.FlujoCierre, MotorConsolidacion.OrigenAjuste, l.Debito - l.Credito, grupo)));
        return new ResultadoOperacionSap(true, $"Asiento {asiento.Numero} contabilizado en {periodo} por {ReglaCuadre.Formato(asiento.Valor)} {moneda}.",
            ResultadoEscritura.Creado, Cantidad: lineas.Count, Clave: asiento.Numero);
    }

    /// <summary>Estado de trabajo (RF-37): bloquea o abre una sociedad en el periodo y lo deja en la auditoría (RNF-Aud-02).</summary>
    private static ResultadoOperacionSap CambiarEstado(JsonElement d, IAlmacenSimulado almacen)
    {
        var sociedad = d.GetProperty("sociedad").GetString()!;
        var periodo = d.GetProperty("periodo").GetString()!;
        var bloquear = d.GetProperty("bloquear").GetBoolean();
        var usuario = d.GetProperty("usuario").GetString()!;
        var fecha = d.GetProperty("fecha").GetDateTimeOffset();
        var antes = almacen.Bloqueada(sociedad, periodo);
        almacen.GuardarBloqueo(new BloqueoBpc(sociedad, periodo, bloquear, usuario, fecha));
        almacen.Auditar([new AuditoriaBpc(fecha, usuario, EstadosTrabajo, sociedad, MotorConsolidacion.PeriodoBpc(periodo), "Estado de trabajo", Estado(antes), Estado(bloquear))]);
        return new ResultadoOperacionSap(true, $"{sociedad} {(bloquear ? "bloqueada" : "desbloqueada")} en {periodo}.", ResultadoEscritura.Modificado);
    }

    public const string EstadosTrabajo = "ESTADOS_TRABAJO";

    public static string Estado(bool bloqueada) => bloqueada ? "Bloqueada" : "Abierta";

    /// <summary>
    /// HU-49 CA4: el paquete que lleva al modelo de planeación el presupuesto (versión anual) y la ejecución (ACTUAL) que el
    /// ERP dejó en BW (0FI_GL_14), por cuenta, centro y mes. Reemplaza la carga anterior del ERP; lo capturado en P05 se conserva.
    /// </summary>
    private static ResultadoOperacionSap CargarPresupuesto(JsonElement d, IAlmacenSimulado almacen)
    {
        var anio = d.GetProperty("anio").GetInt32();
        var version = d.GetProperty("versionAnual").GetString()!;
        var nuevos = new List<RegistroBpc>();
        var sociedades = d.GetProperty("sociedades").EnumerateArray().Select(x => (Codigo: x.GetProperty("codigo").GetString()!, Plan: x.GetProperty("plan").GetString()!)).ToList();
        foreach (var (codigo, plan) in sociedades)
        {
            nuevos.AddRange(almacen.PlanErp(codigo, anio).Select(x => new RegistroBpc(Reportes.MotorPresupuesto.Modelo, codigo, Generacion.GeneradorModeloBpc.IdCuenta(plan, x.Cuenta),
                IntcoNinguno, x.Periodo.Replace('-', '.'), MotorConsolidacion.MonedaLocal, "", OrigenErp, x.Importe,
                Categoria: x.Categoria == SimulatedSapGateway.CategoriaReal ? Reportes.MotorPresupuesto.Real : version, Centro: x.Centro)));
        }

        var codigos = sociedades.Select(x => x.Codigo).ToHashSet();
        var prefijo = $"{anio}.";
        almacen.Reemplazar(r => r.Modelo == Reportes.MotorPresupuesto.Modelo && r.Origen == OrigenErp && codigos.Contains(r.Entidad) && r.Periodo.StartsWith(prefijo, StringComparison.Ordinal), nuevos);
        return new ResultadoOperacionSap(true, $"{nuevos.Count} registros de presupuesto y ejecución {anio} cargados en {Reportes.MotorPresupuesto.Modelo}.", ResultadoEscritura.Creado, Cantidad: nuevos.Count);
    }

    /// <summary>
    /// «Guardar datos» de P05 (HU-50 CA3, HU-51 CA2): reemplaza las celdas de esas filas en la versión y el año elegidos; las
    /// demás versiones no cambian.
    /// </summary>
    private static ResultadoOperacionSap GuardarPresupuesto(JsonElement d, IAlmacenSimulado almacen)
    {
        var sociedad = d.GetProperty("sociedad").GetString()!;
        var version = d.GetProperty("version").GetString()!;
        var anio = d.GetProperty("anio").GetInt32();
        var celdas = d.GetProperty("celdas").EnumerateArray().Select(c => (Cuenta: c.GetProperty("cuenta").GetString()!, Centro: c.GetProperty("centro").GetString()!,
            Mes: c.GetProperty("mes").GetInt32(), Importe: c.GetProperty("importe").GetDecimal())).ToList();
        var claves = celdas.Select(c => (c.Cuenta, c.Centro, Periodo: $"{anio}.{c.Mes:00}")).ToHashSet();
        almacen.Reemplazar(
            r => r.Modelo == Reportes.MotorPresupuesto.Modelo && r.Entidad == sociedad && r.Categoria == version && claves.Contains((r.Cuenta, r.Centro, r.Periodo)),
            celdas.Where(c => c.Importe != 0m).Select(c => new RegistroBpc(Reportes.MotorPresupuesto.Modelo, sociedad, c.Cuenta, IntcoNinguno, $"{anio}.{c.Mes:00}",
                MotorConsolidacion.MonedaLocal, "", OrigenCaptura, c.Importe, Categoria: version, Centro: c.Centro)));
        return new ResultadoOperacionSap(true, $"{celdas.Count} celdas de {version} {anio} guardadas para {sociedad}.", ResultadoEscritura.Modificado, Cantidad: celdas.Count);
    }

    public const string OrigenErp = "ERP";
    public const string OrigenCaptura = "P05";

    private const string IntcoNinguno = MotorConsolidacion.IntcoNinguno;
}
