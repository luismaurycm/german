using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Reportes;

/// <summary>Parámetros de UX-11 (HU-42): sociedad, si incluye filiales (RN-09), moneda local o dólares (RN-06) y periodo.</summary>
public sealed record ParametrosReporte(string Sociedad, bool IncluirFiliales, string Moneda, string Periodo);

public sealed record ColumnaReporte(string Id, string Titulo);

public enum TipoFila
{
    Seccion,
    Rubro,
    Detalle,
    Subtotal,
    Total,
}

/// <summary>Una fila del reporte: etiquetas (código, descripción…) y un valor por columna; <see cref="Rubro"/> permite ver su detalle.</summary>
public sealed record FilaReporte(TipoFila Tipo, IReadOnlyList<string> Etiquetas, IReadOnlyList<decimal> Valores, string? Rubro = null);

public sealed record ReporteCalculado(
    DefinicionReporte Definicion,
    ParametrosReporte Parametros,
    IReadOnlyList<string> Encabezados,
    IReadOnlyList<ColumnaReporte> Columnas,
    IReadOnlyList<FilaReporte> Filas,
    IReadOnlyList<string> Avisos,
    IReadOnlyList<string> Errores,
    bool SinDatos = false)
{
    /// <summary>UX-11 vacío: «Sin datos para la sociedad y el periodo elegidos».</summary>
    public bool Vacio => SinDatos || !Filas.Any(f => f.Tipo is TipoFila.Rubro or TipoFila.Detalle);

    public FilaReporte? Fila(string etiqueta) => Filas.FirstOrDefault(f => f.Etiquetas.Contains(etiqueta));
}

/// <summary>Lo que el reporte lee de BPC y del acelerador: datos del periodo y del anterior en la moneda, mapeo y perímetro.</summary>
public sealed record DatosReporte(
    IReadOnlyList<RegistroBpc> Registros,
    IReadOnlyList<RegistroBpc> Anteriores,
    IReadOnlyList<AsignacionCuenta> Asignaciones,
    IReadOnlyList<Sociedad> Sociedades,
    IReadOnlyList<Participacion> Vigentes,
    string Grupo);

/// <summary>Actividad del estado de flujos de efectivo a la que va cada rubro del balance (R04, método indirecto).</summary>
public enum ActividadFlujo
{
    Efectivo,
    Resultado,
    Operacion,
    Inversion,
    Financiacion,
    Conversion,
}

/// <summary>HU-45 CA3: clasificación de los rubros del flujo de efectivo; un rubro sin clasificar es un error de R04.</summary>
public static class ReglasFlujo
{
    public static readonly IReadOnlyDictionary<string, ActividadFlujo> Clasificacion = new Dictionary<string, ActividadFlujo>
    {
        ["F-00-101"] = ActividadFlujo.Efectivo,
        ["F-00-102"] = ActividadFlujo.Operacion,
        ["F-00-103"] = ActividadFlujo.Inversion,
        ["F-00-106"] = ActividadFlujo.Operacion,
        ["F-00-107"] = ActividadFlujo.Inversion,
        ["F-00-108"] = ActividadFlujo.Inversion,
        ["F-00-201"] = ActividadFlujo.Operacion,
        ["F-00-202"] = ActividadFlujo.Financiacion,
        ["F-00-203"] = ActividadFlujo.Operacion,
        ["F-00-301"] = ActividadFlujo.Financiacion,
        ["F-00-302"] = ActividadFlujo.Resultado,
        ["F-00-303"] = ActividadFlujo.Conversion,
        ["F-00-304"] = ActividadFlujo.Financiacion,
    };

    /// <summary>Rubros de balance mapeados sin actividad de flujo (p. ej. F-00-104 Otros activos, que el cliente debe clasificar).</summary>
    public static IReadOnlyList<string> SinClasificar(IEnumerable<string> rubros) =>
        rubros.Where(r => ReglasRubros.EsBalance(ReglasRubros.Clase(r)) && !Clasificacion.ContainsKey(r)).Distinct().Order(StringComparer.Ordinal).ToList();

    public static string Error(IReadOnlyList<string> sinClasificar) =>
        $"Rubros de flujo sin clasificar: {string.Join(", ", sinClasificar.Select(r => $"{r} {CatalogoRubros.Buscar(r)?.Nombre}".Trim()))}";
}

/// <summary>
/// Cálculo de los reportes de UX-11 sobre el modelo de consolidación (RF-20 a RF-22). Cada rubro es la suma de las cuentas
/// mapeadas en UX-06 (RN-10), con signo de presentación: activos y gastos como están; pasivos, patrimonio e ingresos al revés.
/// Con filiales hay una columna por sociedad integrada, las eliminaciones del grupo y el total consolidado (RN-09).
/// </summary>
public static class MotorReportes
{
    public const string ColumnaEliminaciones = "ELIM";
    public const string ColumnaTotal = "TOTAL";
    public const string SinDatos = "Sin datos para la sociedad y el periodo elegidos.";

    public static ReporteCalculado Calcular(DefinicionReporte d, ParametrosReporte p, DatosReporte datos)
    {
        var ctx = new Contexto(p, datos);
        return d.Id switch
        {
            "R01" => R01(d, ctx),
            "R02" => R02(d, ctx),
            "R03" => R03(d, ctx),
            "R04" => R04(d, ctx),
            "R05" => R05(d, ctx),
            "R06" => R06(d, ctx),
            "R07" => R07(d, ctx),
            _ => throw new ArgumentOutOfRangeException(nameof(d), d.Id, "Reporte sin cálculo en esta versión."),
        };
    }

    /// <summary>HU-43 CA3: las cuentas que forman un rubro, con su valor en cada columna.</summary>
    public static ReporteCalculado Detalle(DefinicionReporte d, ParametrosReporte p, DatosReporte datos, string rubro)
    {
        var ctx = new Contexto(p, datos);
        var signo = Signo(rubro);
        var filas = ctx.CuentasDe(rubro)
            .Select(cuenta => new FilaReporte(TipoFila.Detalle, [cuenta, ctx.Descripcion(cuenta)], ctx.Columnas.Select(c => signo * ctx.Saldo(c.Id, cuenta)).ToList()))
            .ToList();
        filas.Add(new FilaReporte(TipoFila.Total, [rubro, CatalogoRubros.Buscar(rubro)?.Nombre ?? rubro], ctx.Columnas.Select(c => signo * ctx.SaldoRubro(c.Id, rubro)).ToList(), rubro));
        return new ReporteCalculado(d, p, ["Cuenta", "Descripción"], ctx.Columnas, filas, [], [], ctx.SinDatos);
    }

    /// <summary>Signo de presentación del rubro: activo y gasto positivos con saldo débito; los demás con saldo crédito.</summary>
    public static int Signo(string rubro) => ReglasRubros.Clase(rubro) is ClaseCuenta.Activo or ClaseCuenta.Gasto ? 1 : -1;

    private static ReporteCalculado R01(DefinicionReporte d, Contexto ctx)
    {
        var filas = new List<FilaReporte>();
        var activo = Seccion(ctx, filas, "Activo", "F-00-1", "Total activo");
        var pasivo = Seccion(ctx, filas, "Pasivo", "F-00-2", "Total pasivo");
        var patrimonio = Seccion(ctx, filas, "Patrimonio", "F-00-3", "Total patrimonio");
        filas.Add(new FilaReporte(TipoFila.Total, ["", "Total pasivo y patrimonio"], Sumar(pasivo, patrimonio)));
        var avisos = ctx.Avisos().ToList();
        if (activo.Zip(Sumar(pasivo, patrimonio)).Any(x => x.First != x.Second))
        {
            avisos.Add("El activo no es igual al pasivo más el patrimonio: revisa C03 y el mapeo (RN-16).");
        }

        return Resultado(d, ctx, ["Rubro", "Descripción"], ctx.Columnas, filas, avisos);
    }

    private static ReporteCalculado R02(DefinicionReporte d, Contexto ctx)
    {
        var filas = new List<FilaReporte>();
        var ingresos = Seccion(ctx, filas, "Ingresos", "F-00-4", "Total ingresos");
        var gastos = Seccion(ctx, filas, "Gastos", "F-00-5", "Total gastos");
        var utilidad = ingresos.Zip(gastos, (i, g) => i - g).ToList();
        filas.Add(new FilaReporte(TipoFila.Total, ["", "Utilidad neta del ejercicio"], utilidad));
        var ori = ctx.Columnas.Select(c => -(ctx.SaldoRubro(c.Id, ReglasRubros.CuentaDiferenciaConversion.Rubro) - ctx.SaldoRubro(c.Id, ReglasRubros.CuentaDiferenciaConversion.Rubro, anterior: true))).ToList();
        filas.Add(new FilaReporte(TipoFila.Rubro, [ReglasRubros.CuentaDiferenciaConversion.Rubro, "Otro resultado integral: diferencia en conversión"], ori, ReglasRubros.CuentaDiferenciaConversion.Rubro));
        filas.Add(new FilaReporte(TipoFila.Total, ["", "Resultado integral total"], Sumar(utilidad, ori)));
        var minoritario = ctx.Columnas.Select(c => ctx.Saldo(c.Id, ReglasRubros.CuentaResultadoMinoritario.Id)).ToList();
        filas.Add(new FilaReporte(TipoFila.Seccion, ["", "Utilidad neta atribuible a"], Ceros(ctx)));
        filas.Add(new FilaReporte(TipoFila.Detalle, ["", "Propietarios de la controladora"], utilidad.Zip(minoritario, (u, m) => u - m).ToList()));
        filas.Add(new FilaReporte(TipoFila.Detalle, [ReglasRubros.CuentaResultadoMinoritario.Id, "Interés minoritario"], minoritario));
        return Resultado(d, ctx, ["Rubro", "Descripción"], ctx.Columnas, filas, ctx.Avisos().ToList());
    }

    /// <summary>R03: cada componente del patrimonio (con el interés minoritario y la diferencia en conversión por separado) al cierre anterior, su movimiento y su saldo.</summary>
    private static ReporteCalculado R03(DefinicionReporte d, Contexto ctx)
    {
        var anterior = ModeloPropiedad.PeriodoAnterior(MotorConsolidacion.PeriodoBpc(ctx.Parametros.Periodo)).Replace('.', '-');
        ColumnaReporte[] columnas = [new("ANT", $"Saldo al cierre de {anterior}"), new("MOV", "Movimiento del periodo"), new("CIE", $"Saldo al cierre de {ctx.Parametros.Periodo}")];
        var total = ctx.ColumnaPerimetro;
        var rubros = ctx.Rubros("F-00-3").Union([ReglasRubros.CuentaDiferenciaConversion.Rubro, ReglasRubros.CuentaInteresMinoritario.Rubro]).Order(StringComparer.Ordinal).ToList();
        var filas = new List<FilaReporte>();
        foreach (var r in rubros)
        {
            var ant = -ctx.SaldoRubro(total, r, anterior: true);
            var cie = -ctx.SaldoRubro(total, r);
            filas.Add(new FilaReporte(TipoFila.Rubro, [r, CatalogoRubros.Buscar(r)?.Nombre ?? r], [ant, cie - ant, cie], r));
        }

        filas.Add(new FilaReporte(TipoFila.Total, ["", "Total patrimonio"], Enumerable.Range(0, 3).Select(i => filas.Sum(f => f.Valores[i])).ToList()));
        return Resultado(d, ctx, ["Rubro", "Componente del patrimonio"], columnas, filas, ctx.Avisos().ToList());
    }

    /// <summary>R04: método indirecto; cada rubro aporta la variación de su saldo frente al cierre anterior en su actividad.</summary>
    private static ReporteCalculado R04(DefinicionReporte d, Contexto ctx)
    {
        ColumnaReporte[] columnas = [new("VAL", ctx.Parametros.Periodo)];
        var total = ctx.ColumnaPerimetro;
        decimal Variacion(string rubro) => -(ctx.SaldoRubro(total, rubro) - ctx.SaldoRubro(total, rubro, anterior: true));
        var rubros = ctx.RubrosConSaldo().Where(r => ReglasRubros.EsBalance(ReglasRubros.Clase(r))).ToList();
        var errores = new List<string>();
        var sinClasificar = ReglasFlujo.SinClasificar(rubros.Where(r => Variacion(r) != 0m));
        if (sinClasificar.Count > 0)
        {
            errores.Add(ReglasFlujo.Error(sinClasificar));
        }

        var filas = new List<FilaReporte>();
        var subtotales = new List<decimal>();
        foreach (var (actividad, titulo) in new[] { (ActividadFlujo.Operacion, "operación"), (ActividadFlujo.Inversion, "inversión"), (ActividadFlujo.Financiacion, "financiación") })
        {
            filas.Add(new FilaReporte(TipoFila.Seccion, ["", $"Actividades de {titulo}"], [0m]));
            var deActividad = rubros.Where(r => ReglasFlujo.Clasificacion.GetValueOrDefault(r, (ActividadFlujo)(-1)) == actividad
                || (actividad == ActividadFlujo.Operacion && ReglasFlujo.Clasificacion.GetValueOrDefault(r, (ActividadFlujo)(-1)) == ActividadFlujo.Resultado)).ToList();
            foreach (var r in deActividad.OrderBy(r => ReglasFlujo.Clasificacion[r] == ActividadFlujo.Resultado ? 0 : 1).ThenBy(r => r, StringComparer.Ordinal))
            {
                filas.Add(new FilaReporte(TipoFila.Rubro, [r, CatalogoRubros.Buscar(r)?.Nombre ?? r], [Variacion(r)], r));
            }

            var sub = deActividad.Sum(Variacion);
            subtotales.Add(sub);
            filas.Add(new FilaReporte(TipoFila.Subtotal, ["", $"Flujo neto de actividades de {titulo}"], [sub]));
        }

        var efecto = rubros.Where(r => ReglasFlujo.Clasificacion.GetValueOrDefault(r, (ActividadFlujo)(-1)) == ActividadFlujo.Conversion).Sum(Variacion);
        filas.Add(new FilaReporte(TipoFila.Rubro, [ReglasRubros.CuentaDiferenciaConversion.Rubro, "Efecto de la diferencia en conversión"], [efecto], ReglasRubros.CuentaDiferenciaConversion.Rubro));
        var variacion = subtotales.Sum() + efecto;
        var inicio = ctx.SaldoRubro(total, "F-00-101", anterior: true);
        filas.Add(new FilaReporte(TipoFila.Total, ["", "Variación neta del efectivo"], [variacion]));
        filas.Add(new FilaReporte(TipoFila.Detalle, ["F-00-101", "Efectivo al inicio del periodo"], [inicio]));
        filas.Add(new FilaReporte(TipoFila.Total, ["F-00-101", "Efectivo al cierre del periodo"], [inicio + variacion]));
        var avisos = ctx.Avisos().ToList();
        if (errores.Count == 0 && inicio + variacion != ctx.SaldoRubro(total, "F-00-101"))
        {
            avisos.Add("El efectivo al cierre no coincide con el saldo de F-00-101: revisa el mapeo y C03.");
        }

        return Resultado(d, ctx, ["Rubro", "Concepto"], columnas, filas, avisos, errores);
    }

    /// <summary>R05: lo eliminado por pareja de sociedades (cuenta por cuenta) y la diferencia que quedó en ZACC_DIF_IC; luego la eliminación de inversión.</summary>
    private static ReporteCalculado R05(DefinicionReporte d, Contexto ctx)
    {
        ColumnaReporte[] columnas = [new("ELIM", "Saldo eliminado")];
        var elim = ctx.Datos.Registros.Where(r => r.Grupo == ctx.Datos.Grupo && r.Origen == MotorConsolidacion.OrigenEliminacionIc
                                                  && r.Cuenta != ReglasRubros.CuentaDiferenciaIntercompania.Id && ctx.Entidades.Contains(r.Entidad)).ToList();
        var filas = new List<FilaReporte>();
        foreach (var pareja in elim.GroupBy(r => Pareja(r.Entidad, r.Intco[2..])).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            filas.Add(new FilaReporte(TipoFila.Seccion, ["", "", $"Pareja {pareja.Key}"], [0m]));
            foreach (var g in pareja.GroupBy(r => (r.Entidad, Socio: r.Intco[2..], r.Cuenta)).OrderBy(g => g.Key.Entidad, StringComparer.Ordinal).ThenBy(g => g.Key.Cuenta, StringComparer.Ordinal))
            {
                filas.Add(new FilaReporte(TipoFila.Detalle, [g.Key.Entidad, g.Key.Socio, g.Key.Cuenta], [-g.Sum(r => r.Importe)]));
            }

            filas.Add(new FilaReporte(TipoFila.Subtotal, ["", "", $"Diferencia de la pareja en {ReglasRubros.CuentaDiferenciaIntercompania.Id}"], [-pareja.Sum(r => r.Importe)]));
        }

        var inversion = ctx.Datos.Registros.Where(r => r.Grupo == ctx.Datos.Grupo && r.Origen == MotorConsolidacion.OrigenEliminacionInversion && ctx.Entidades.Contains(r.Entidad)).ToList();
        if (inversion.Count > 0)
        {
            filas.Add(new FilaReporte(TipoFila.Seccion, ["", "", "Eliminación de inversión e interés minoritario"], [0m]));
            foreach (var g in inversion.GroupBy(r => (r.Entidad, Socio: r.Intco == MotorConsolidacion.IntcoNinguno ? "—" : r.Intco[2..], r.Cuenta))
                         .OrderBy(g => g.Key.Entidad, StringComparer.Ordinal).ThenBy(g => g.Key.Cuenta, StringComparer.Ordinal))
            {
                filas.Add(new FilaReporte(TipoFila.Detalle, [g.Key.Entidad, g.Key.Socio, g.Key.Cuenta], [g.Sum(r => r.Importe)]));
            }
        }

        return Resultado(d, ctx, ["Sociedad", "Contraparte", "Cuenta"], columnas, filas, []);
    }

    /// <summary>R06: saldos y transacciones de cada sociedad con sus partes relacionadas (subsidiarias, asociadas y matriz), antes de eliminar.</summary>
    private static ReporteCalculado R06(DefinicionReporte d, Contexto ctx)
    {
        ColumnaReporte[] columnas = [new("SALDO", "Saldo")];
        var partidas = ctx.Datos.Registros.Where(r => r.Grupo.Length == 0 && r.Origen == MotorConsolidacion.OrigenCarga && r.Intco != MotorConsolidacion.IntcoNinguno
                                                      && r.Intco.StartsWith("I_", StringComparison.Ordinal) && ctx.Relacionadas.Contains(r.Entidad)).ToList();
        var filas = partidas
            .GroupBy(r => (r.Entidad, Parte: r.Intco[2..], Naturaleza: ReglasRubros.EsResultados(ReglasRubros.Clase(ctx.RubroDe(r.Cuenta) ?? "F-00-1")) ? ReglasConciliacion.Resultados : ReglasConciliacion.Balance))
            .OrderBy(g => g.Key.Entidad, StringComparer.Ordinal).ThenBy(g => g.Key.Parte, StringComparer.Ordinal).ThenBy(g => g.Key.Naturaleza, StringComparer.Ordinal)
            .Select(g => new FilaReporte(TipoFila.Detalle,
                [g.Key.Entidad, g.Key.Parte, ctx.Relacion(g.Key.Parte), g.Key.Naturaleza, string.Join(", ", g.Select(r => r.Cuenta).Distinct().Order(StringComparer.Ordinal))],
                [g.Sum(r => r.Importe)]))
            .ToList();
        return Resultado(d, ctx, ["Sociedad", "Parte relacionada", "Relación", "Naturaleza", "Cuentas"], columnas, filas, []);
    }

    /// <summary>R07: las cuentas mapeadas a F-00-101 con su saldo y el total del rubro.</summary>
    private static ReporteCalculado R07(DefinicionReporte d, Contexto ctx)
    {
        const string rubro = "F-00-101";
        var filas = ctx.CuentasDe(rubro)
            .Select(cuenta => new FilaReporte(TipoFila.Detalle, [cuenta, ctx.Descripcion(cuenta)], ctx.Columnas.Select(c => ctx.Saldo(c.Id, cuenta)).ToList()))
            .ToList();
        filas.Add(new FilaReporte(TipoFila.Total, [rubro, CatalogoRubros.Buscar(rubro)!.Nombre], ctx.Columnas.Select(c => ctx.SaldoRubro(c.Id, rubro)).ToList(), rubro));
        return Resultado(d, ctx, ["Cuenta", "Descripción"], ctx.Columnas, filas, ctx.Avisos().ToList());
    }

    private static string Pareja(string a, string b) => string.CompareOrdinal(a, b) < 0 ? $"{a} ↔ {b}" : $"{b} ↔ {a}";

    private static List<decimal> Seccion(Contexto ctx, List<FilaReporte> filas, string titulo, string prefijo, string total)
    {
        filas.Add(new FilaReporte(TipoFila.Seccion, ["", titulo], Ceros(ctx)));
        var suma = Ceros(ctx).ToList();
        foreach (var r in ctx.Rubros(prefijo))
        {
            var valores = ctx.Columnas.Select(c => Signo(r) * ctx.SaldoRubro(c.Id, r)).ToList();
            filas.Add(new FilaReporte(TipoFila.Rubro, [r, CatalogoRubros.Buscar(r)?.Nombre ?? r], valores, r));
            suma = Sumar(suma, valores);
        }

        filas.Add(new FilaReporte(TipoFila.Subtotal, ["", total], suma));
        return suma;
    }

    private static List<decimal> Ceros(Contexto ctx) => ctx.Columnas.Select(_ => 0m).ToList();

    private static List<decimal> Sumar(IReadOnlyList<decimal> a, IReadOnlyList<decimal> b) => a.Zip(b, (x, y) => x + y).ToList();

    private static ReporteCalculado Resultado(DefinicionReporte d, Contexto ctx, IReadOnlyList<string> encabezados, IReadOnlyList<ColumnaReporte> columnas, List<FilaReporte> filas, List<string> avisos, List<string>? errores = null) =>
        new(d, ctx.Parametros, encabezados, columnas, filas, ctx.SinDatos ? [] : avisos, errores ?? [], ctx.SinDatos);

    /// <summary>El perímetro del reporte, las columnas y los saldos por columna y cuenta (periodo y periodo anterior).</summary>
    private sealed class Contexto
    {
        private readonly Dictionary<string, Dictionary<string, decimal>> _saldos = [];
        private readonly Dictionary<string, Dictionary<string, decimal>> _anteriores = [];
        private readonly IReadOnlyDictionary<string, string> _rubroPorCuenta;

        public Contexto(ParametrosReporte p, DatosReporte datos)
        {
            Parametros = p;
            Datos = datos;
            _rubroPorCuenta = datos.Asignaciones.ToDictionary(a => GeneradorModeloBpc.IdCuenta(a.PlanCuentas, a.Cuenta), a => a.Rubro)
                .Concat(ReglasRubros.CuentasTecnicas.Select(t => KeyValuePair.Create(t.Id, t.Rubro)))
                .GroupBy(k => k.Key).ToDictionary(g => g.Key, g => g.First().Value);
            MetodoConsolidacion Metodo(string s) => datos.Vigentes.FirstOrDefault(x => x.Sociedad == s)?.Metodo
                                                    ?? datos.Sociedades.FirstOrDefault(x => x.Codigo == s)?.Metodo ?? MetodoConsolidacion.FueraDelPerimetro;
            _metodo = Metodo;
            var matriz = datos.Sociedades.Count == 0 ? "" : ReglasCierre.Matriz(datos.Sociedades).Codigo;
            var entidades = new List<string> { p.Sociedad };
            if (p.IncluirFiliales)
            {
                var pendientes = new Queue<string>([p.Sociedad]);
                while (pendientes.TryDequeue(out var actual))
                {
                    foreach (var hija in datos.Sociedades.Where(s => s.Matriz == actual && s.Codigo != actual).Select(s => s.Codigo))
                    {
                        if (Metodo(hija) is MetodoConsolidacion.IntegracionGlobal && !entidades.Contains(hija))
                        {
                            entidades.Add(hija);
                            pendientes.Enqueue(hija);
                        }
                    }
                }
            }

            Entidades = entidades;
            Consolida = p.IncluirFiliales && p.Sociedad == matriz;
            var columnas = entidades.Select(e => new ColumnaReporte(e, $"{e} {datos.Sociedades.FirstOrDefault(s => s.Codigo == e)?.Nombre}".Trim())).ToList();
            if (Consolida)
            {
                columnas.Add(new ColumnaReporte(ColumnaEliminaciones, "Eliminaciones y ajustes"));
            }

            if (columnas.Count > 1)
            {
                columnas.Add(new ColumnaReporte(ColumnaTotal, Consolida ? "Total consolidado" : "Total"));
            }

            Columnas = columnas;
            Relacionadas = p.IncluirFiliales ? datos.Sociedades.Select(s => s.Codigo).Where(s => s == p.Sociedad || entidades.Contains(s) || Metodo(s) == MetodoConsolidacion.Participacion).ToHashSet() : [p.Sociedad];
            Cargar(_saldos, datos.Registros);
            Cargar(_anteriores, datos.Anteriores);
        }

        private readonly Func<string, MetodoConsolidacion> _metodo;

        public ParametrosReporte Parametros { get; }

        public DatosReporte Datos { get; }

        public IReadOnlyList<string> Entidades { get; }

        public IReadOnlySet<string> Relacionadas { get; }

        public bool Consolida { get; }

        public IReadOnlyList<ColumnaReporte> Columnas { get; }

        /// <summary>La columna que representa el perímetro completo (el total, o la única sociedad).</summary>
        public string ColumnaPerimetro => Columnas[^1].Id;

        public bool SinDatos => Datos.Registros.All(r => !Entidades.Contains(r.Entidad) || r.Moneda != Parametros.Moneda);

        public string? RubroDe(string cuenta) => _rubroPorCuenta.GetValueOrDefault(cuenta);

        public string Relacion(string sociedad) => _metodo(sociedad) switch
        {
            MetodoConsolidacion.Matriz => "Matriz",
            MetodoConsolidacion.IntegracionGlobal => "Subsidiaria",
            MetodoConsolidacion.Participacion => "Asociada",
            _ => "Vinculada fuera del perímetro",
        };

        public string Descripcion(string cuenta)
        {
            var tecnica = ReglasRubros.CuentasTecnicas.FirstOrDefault(t => t.Id == cuenta);
            if (tecnica is not null)
            {
                return tecnica.Descripcion;
            }

            var i = cuenta.IndexOf('_', StringComparison.Ordinal);
            return i > 0 ? $"Cuenta {cuenta[(i + 1)..]} del plan {cuenta[..i]}" : cuenta;
        }

        public decimal Saldo(string columna, string cuenta, bool anterior = false) =>
            (anterior ? _anteriores : _saldos).GetValueOrDefault(columna)?.GetValueOrDefault(cuenta) ?? 0m;

        public decimal SaldoRubro(string columna, string rubro, bool anterior = false) =>
            (anterior ? _anteriores : _saldos).GetValueOrDefault(columna)?.Where(k => RubroDe(k.Key) == rubro).Sum(k => k.Value) ?? 0m;

        /// <summary>Rubros del prefijo (F-00-1 activo…) que tienen cuentas mapeadas o saldo, en el orden del catálogo.</summary>
        public IReadOnlyList<string> Rubros(string prefijo)
        {
            var usados = _rubroPorCuenta.Where(k => Datos.Asignaciones.Any(a => a.Rubro == k.Value) || _saldos.Values.Any(c => c.ContainsKey(k.Key)))
                .Select(k => k.Value).Where(r => r.StartsWith(prefijo, StringComparison.Ordinal)).ToHashSet();
            return CatalogoRubros.Todos.Select(r => r.Codigo).Where(usados.Contains).Concat(usados.Except(CatalogoRubros.Todos.Select(r => r.Codigo)).Order(StringComparer.Ordinal)).ToList();
        }

        public IReadOnlyList<string> RubrosConSaldo() =>
            _saldos.Values.Concat(_anteriores.Values).SelectMany(c => c.Keys).Select(RubroDe).OfType<string>().Distinct().Order(StringComparer.Ordinal).ToList();

        public IReadOnlyList<string> CuentasDe(string rubro) =>
            _rubroPorCuenta.Where(k => k.Value == rubro && (Datos.Asignaciones.Any(a => GeneradorModeloBpc.IdCuenta(a.PlanCuentas, a.Cuenta) == k.Key) || _saldos.Values.Any(c => c.ContainsKey(k.Key))))
                .Select(k => k.Key).Order(StringComparer.Ordinal).ToList();

        /// <summary>RN-10: aviso de cuentas con saldo que no suman en ningún rubro.</summary>
        public IEnumerable<string> Avisos()
        {
            var sinRubro = _saldos.Values.SelectMany(c => c.Where(k => k.Value != 0m).Select(k => k.Key)).Where(k => RubroDe(k) is null).Distinct().ToList();
            if (sinRubro.Count > 0)
            {
                yield return $"{Textos.Cantidad(sinRubro.Count, "cuenta con saldo no está mapeada", "cuentas con saldo no están mapeadas")} a un rubro y no suma en el reporte (RN-10): {string.Join(", ", sinRubro.Order(StringComparer.Ordinal).Take(5))}.";
            }
        }

        private void Cargar(Dictionary<string, Dictionary<string, decimal>> destino, IEnumerable<RegistroBpc> registros)
        {
            foreach (var r in registros.Where(r => r.Modelo == MotorConsolidacion.Modelo && r.Moneda == Parametros.Moneda))
            {
                string? columna = r.Grupo.Length == 0 ? (Entidades.Contains(r.Entidad) ? r.Entidad : null)
                    : Consolida && r.Grupo == Datos.Grupo ? ColumnaEliminaciones : null;
                if (columna is null)
                {
                    continue;
                }

                Sumar(destino, columna, r.Cuenta, r.Importe);
                if (Columnas.Count > 1)
                {
                    Sumar(destino, ColumnaTotal, r.Cuenta, r.Importe);
                }
            }
        }

        private static void Sumar(Dictionary<string, Dictionary<string, decimal>> destino, string columna, string cuenta, decimal importe)
        {
            if (!destino.TryGetValue(columna, out var c))
            {
                destino[columna] = c = [];
            }

            c[cuenta] = c.GetValueOrDefault(cuenta) + importe;
        }
    }
}
