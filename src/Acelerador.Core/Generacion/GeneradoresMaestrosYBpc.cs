using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>
/// HU-21 a HU-24 (cargador de datos maestros): planes de cuentas, centros de costo y de beneficio, monedas y tasas, y
/// jerarquía de cuentas. Cada carga la hace el paquete Z en BW leyendo del ERP por el sistema fuente (INT-01), y
/// devuelve la cantidad cargada que UX-09 muestra en el paso.
/// </summary>
public sealed class GeneradorDatosMaestros : IGeneradorObjetos
{
    public const string Funcion = "Z_ACC_CARGAR_MAESTRO";
    public const string Tipo = "Carga de datos maestros";

    public bool Aplica(ContextoPlan c) => ReglasBw.TieneBodega(c.Cliente);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var enSap = c.Sociedades.Where(s => s.EnSap).ToList();
        var planes = enSap.GroupBy(s => s.PlanCuentas).Select(g => (Plan: g.Key, Sociedades: g.Select(s => s.Codigo).ToList())).ToList();

        // HU-21: el plan de cuentas de cada sociedad (RN-05); las sociedades que comparten plan lo cargan una vez.
        foreach (var (plan, sociedades) in planes)
        {
            yield return Carga(PasosGeneracion.PlanesCuentas, "Planes de cuentas", $"Plan {plan}", $"Plan de cuentas {plan} de {string.Join(", ", sociedades)} (0GL_ACCOUNT)",
                new { tipo = "PLAN_CUENTAS", plan, sociedades, infoObject = "0GL_ACCOUNT" });
        }

        // HU-22: centros de costo y de beneficio de cada sociedad seleccionada.
        foreach (var s in enSap)
        {
            yield return Carga(PasosGeneracion.CentrosCosto, "Centros de costo y de beneficio", $"CeCo y CeBe {s.Codigo}",
                $"Centros de costo y de beneficio de {s.Codigo} (0COSTCENTER, 0PROFIT_CTR)",
                new { tipo = "CENTROS", sociedades = new[] { s.Codigo }, infoObjects = new[] { "0COSTCENTER", "0PROFIT_CTR" } });
        }

        // HU-23: la moneda local tal como la maneja SAP (RN-06) y las tasas de cierre, promedio e histórica (RN-11).
        yield return Carga(PasosGeneracion.Monedas, "Monedas y tasas", "Monedas locales", "Moneda local de cada sociedad desde T001",
            new { tipo = "MONEDAS", sociedades = c.Sociedades.Select(s => s.Codigo), tabla = "T001" });
        var periodos = ReglasMonedas.PeriodosCarga(c.Fecha);
        foreach (var moneda in c.Sociedades.Select(s => s.MonedaLocal).Where(m => m != ReglasMonedas.Dolar).Distinct().Order(StringComparer.Ordinal))
        {
            yield return Carga(PasosGeneracion.Monedas, "Monedas y tasas", $"Tasas {moneda}/USD",
                $"Tasas de cierre, promedio e histórica {moneda}/USD de {periodos.FirstOrDefault()} a {periodos.LastOrDefault()} (TCURR)",
                new { tipo = "TASAS", moneda, contra = ReglasMonedas.Dolar, periodos, tipos = ReglasMonedas.TiposTcurr });
        }

        // HU-24: la jerarquía de cuentas de cada plan (estructura de balance del ERP).
        foreach (var (plan, sociedades) in planes)
        {
            yield return Carga(PasosGeneracion.Jerarquia, "Jerarquía de cuentas", $"Jerarquía {plan}", $"Jerarquía de cuentas del plan {plan} (0GL_ACCOUNT)",
                new { tipo = "JERARQUIA", plan, sociedades, infoObject = "0GL_ACCOUNT" });
        }
    }

    private static ObjetoPlan Carga(string paso, string grupo, string nombre, string descripcion, object definicion) =>
        new(paso, Capa.Bw, grupo, SistemaSap.Bw, Tipo, nombre, descripcion, EstadoObjeto.Nuevo,
            new OperacionSap(Funcion, "CARGA", nombre, Definicion.Json(definicion)));
}

/// <summary>Una dimensión del modelo de consolidación con su tipo de BPC.</summary>
public sealed record DimensionBpc(string Nombre, string Tipo, string Descripcion);

/// <summary>
/// HU-26: modelo de consolidación en BPC modelo estándar (RN-02, variante de la arquitectura A). Respalda el ambiente con
/// UJBR antes de tocarlo (ADR-06), crea el ambiente, las dimensiones y sus miembros con los datos maestros del cliente,
/// y el modelo. Los rubros son los padres de las cuentas en ACCOUNT, así el valor del rubro es la suma de sus cuentas (RN-10).
/// </summary>
public sealed class GeneradorModeloBpc : IGeneradorObjetos
{
    public const string Ambiente = "ZACC";
    public const string Modelo = "CONSOLIDACION";
    public const string Grupo = "Modelo de consolidación";

    /// <summary>Dimensiones del modelo de consolidación (BPC estándar, tipo de modelo Consolidación).</summary>
    public static readonly IReadOnlyList<DimensionBpc> Dimensiones =
    [
        new("ACCOUNT", "A", "Cuentas y rubros"),
        new("ENTITY", "E", "Sociedades del grupo"),
        new("INTCO", "I", "Intercompañía"),
        new("TERCERO", "U", "Terceros"),
        new("TIME", "T", "Periodos"),
        new("CATEGORY", "C", "Categoría (real)"),
        new("RPTCURRENCY", "R", "Moneda de presentación"),
        new("FLOW", "S", "Flujos (apertura, movimiento, diferencia en conversión, cierre)"),
        new("AUDITTRAIL", "D", "Origen del dato (carga, eliminaciones, ajustes, conversión)"),
        new("GROUP", "G", "Grupos de consolidación"),
    ];

    public bool Aplica(ContextoPlan c) => c.Cliente.Modulos.Contains(Modulo.Consolidacion);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var variante = $"Variante para modelo {(c.Variante.Modelo == ModeloBpc.Estandar ? "estándar" : "embebido")} ({c.Variante.Id}). RN-02";
        yield return Objeto(c, "Respaldo UJBR", $"{Ambiente}_RESPALDO", $"Respaldo UJBR del ambiente {Ambiente} antes de modificarlo", "Z_ACC_BPC_UJBR", "UJBR",
            new { modo = "RESPALDO", ambiente = Ambiente }, "Si el ambiente no existe todavía, no hay nada que respaldar.");
        yield return Objeto(c, "Ambiente", Ambiente, "Ambiente BPC del acelerador", "Z_ACC_BPC_CREAR_AMBIENTE", "AMBIENTE",
            new { ambiente = Ambiente, plantilla = "EnvironmentShell" }, variante);

        foreach (var d in Dimensiones)
        {
            yield return Objeto(c, "Dimensión", d.Nombre, d.Descripcion, "Z_ACC_BPC_CREAR_DIMENSION", "DIMENSION",
                new { ambiente = Ambiente, dimension = d.Nombre, tipo = d.Tipo });
        }

        foreach (var (dimension, miembros) in Miembros(c))
        {
            yield return Objeto(c, "Miembros", dimension, $"{Textos.Cantidad(miembros.Count, "miembro", "miembros")} de {dimension} desde los datos maestros",
                FuncionesZ.BpcCrearMiembro, "MIEMBROS", new { ambiente = Ambiente, dimension, miembros });
        }

        yield return Objeto(c, "Modelo", Modelo, "Modelo de consolidación (tipo Consolidación) con sus dimensiones", "Z_ACC_BPC_CREAR_MODELO", "MODELO",
            new { ambiente = Ambiente, modelo = Modelo, tipo = "CONSOLIDACION", dimensiones = Dimensiones.Select(d => d.Nombre) }, variante);
    }

    /// <summary>Miembros de cada dimensión con los datos del cliente (HU-26 CA2).</summary>
    public static IReadOnlyList<(string Dimension, IReadOnlyList<MiembroBpc> Miembros)> Miembros(ContextoPlan c)
    {
        var cuentas = new List<MiembroBpc>();
        foreach (var rubro in CatalogoRubros.Todos)
        {
            cuentas.Add(new MiembroBpc(rubro.Codigo, rubro.Nombre, null));
        }

        foreach (var a in c.Asignaciones.OrderBy(a => a.PlanCuentas).ThenBy(a => a.Cuenta, StringComparer.Ordinal))
        {
            cuentas.Add(new MiembroBpc(IdCuenta(a.PlanCuentas, a.Cuenta), $"Cuenta {a.Cuenta} del plan {a.PlanCuentas}", a.Rubro));
        }

        var anio = c.Fecha.Year;
        var periodos = Enumerable.Range(1, 12).Select(m => new MiembroBpc($"{anio}.{m:00}", $"{anio}-{m:00}", $"{anio}.TOTAL"))
            .Prepend(new MiembroBpc($"{anio}.TOTAL", $"Año {anio}", null)).ToList();
        var matriz = c.Sociedades.FirstOrDefault(s => s.Metodo == MetodoConsolidacion.Matriz)?.Codigo ?? c.Sociedades[0].Codigo;

        return
        [
            ("ACCOUNT", cuentas),
            ("ENTITY", c.Sociedades.Select(s => new MiembroBpc(s.Codigo, s.Nombre, s.Matriz.Length > 0 ? s.Matriz : null, s.MonedaLocal)).ToList()),
            ("INTCO", c.Sociedades.Select(s => new MiembroBpc($"I_{s.Codigo}", $"Intercompañía con {s.Codigo}", null)).Append(new MiembroBpc("I_NONE", "Sin intercompañía", null)).ToList()),
            ("TERCERO", [new MiembroBpc("T_NONE", "Sin tercero", null)]),
            ("TIME", periodos),
            ("CATEGORY", [new MiembroBpc("ACTUAL", "Real", null)]),
            ("RPTCURRENCY", [new MiembroBpc("LC", "Moneda local", null), new MiembroBpc(ReglasMonedas.Dolar, "Dólares", null)]),
            ("FLOW", [new MiembroBpc("F_OPE", "Apertura", null), new MiembroBpc("F_MOV", "Movimiento", null), new MiembroBpc("F_CTA", "Diferencia en conversión", null), new MiembroBpc("F_CLO", "Cierre", null)]),
            ("AUDITTRAIL", [new MiembroBpc("INPUT", "Carga", null), new MiembroBpc("ELIM_IC", "Eliminación intercompañía", null), new MiembroBpc("ELIM_INV", "Eliminación de inversión", null), new MiembroBpc("AJUSTE", "Asientos de ajuste", null), new MiembroBpc("CONVERSION", "Conversión", null)]),
            ("GROUP", [new MiembroBpc($"G_{matriz}", $"Grupo consolidado {matriz}", null, ReglasMonedas.Dolar)]),
        ];
    }

    /// <summary>Id del miembro de cuenta: plan y cuenta, porque cada sociedad puede tener su plan (RN-05).</summary>
    public static string IdCuenta(string plan, string cuenta) => $"{plan}_{cuenta}";

    private static ObjetoPlan Objeto(ContextoPlan c, string tipo, string nombre, string descripcion, string funcion, string tipoObjeto, object definicion, string? nota = null) =>
        new(PasosGeneracion.ModeloBpc, Capa.Bpc, Grupo, SistemaSap.Bpc, tipo, nombre, descripcion, EstadoObjeto.Nuevo,
            new OperacionSap(funcion, tipoObjeto, nombre, Definicion.Json(definicion)), nota);
}

/// <summary>Un miembro de dimensión de BPC: id, descripción, padre en la jerarquía y moneda (ENTITY y GROUP).</summary>
public sealed record MiembroBpc(string Id, string Descripcion, string? Padre, string? Moneda = null);
