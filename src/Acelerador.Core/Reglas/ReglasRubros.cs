using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>Clase contable de un rubro: define la tasa de conversión (RN-11) y en qué lado del cuadre entra (RN-16).</summary>
public enum ClaseCuenta
{
    Activo,
    Pasivo,
    Patrimonio,
    ResultadoEjercicio,
    Ingreso,
    Gasto,
}

/// <summary>Cuenta técnica que el acelerador crea en ACCOUNT de BPC, con el rubro del que cuelga.</summary>
public sealed record CuentaTecnica(string Id, string Descripcion, string Rubro);

/// <summary>Clases de los rubros y cuentas técnicas del modelo de consolidación.</summary>
public static class ReglasRubros
{
    public const string RubroResultado = "F-00-302";

    /// <summary>RN-18: la diferencia en conversión va a esta cuenta de patrimonio, no a resultados.</summary>
    public static readonly CuentaTecnica CuentaDiferenciaConversion = new("ZACC_CTA", "Diferencia en conversión", "F-00-303");

    /// <summary>Contrapartida de las eliminaciones intercompañía: lo que no concilia entre las partes queda aquí (C01).</summary>
    public static readonly CuentaTecnica CuentaDiferenciaIntercompania = new("ZACC_DIF_IC", "Diferencias intercompañía por conciliar", "F-00-106");

    public static readonly IReadOnlyList<CuentaTecnica> CuentasTecnicas = [CuentaDiferenciaConversion, CuentaDiferenciaIntercompania];

    /// <summary>F-00-1xx activo, 2xx pasivo, 3xx patrimonio (302 resultado del ejercicio), 4xx ingresos y 5xx gastos.</summary>
    public static ClaseCuenta Clase(string rubro) => rubro switch
    {
        RubroResultado => ClaseCuenta.ResultadoEjercicio,
        _ when rubro.StartsWith("F-00-1", StringComparison.Ordinal) => ClaseCuenta.Activo,
        _ when rubro.StartsWith("F-00-2", StringComparison.Ordinal) => ClaseCuenta.Pasivo,
        _ when rubro.StartsWith("F-00-3", StringComparison.Ordinal) => ClaseCuenta.Patrimonio,
        _ when rubro.StartsWith("F-00-4", StringComparison.Ordinal) => ClaseCuenta.Ingreso,
        _ => ClaseCuenta.Gasto,
    };

    public static bool EsBalance(ClaseCuenta c) => c is ClaseCuenta.Activo or ClaseCuenta.Pasivo or ClaseCuenta.Patrimonio or ClaseCuenta.ResultadoEjercicio;

    public static bool EsResultados(ClaseCuenta c) => c is ClaseCuenta.Ingreso or ClaseCuenta.Gasto;

    /// <summary>RN-11: cierre para activo y pasivo, promedio para resultados (y el resultado del ejercicio), histórica para patrimonio.</summary>
    public static string TipoTasa(ClaseCuenta c) => c switch
    {
        ClaseCuenta.Activo or ClaseCuenta.Pasivo => "cierre",
        ClaseCuenta.Patrimonio => "historica",
        _ => "promedio",
    };

    /// <summary>Clase de cada miembro de ACCOUNT (cuentas mapeadas y técnicas), para la lógica de BPC.</summary>
    public static IReadOnlyDictionary<string, ClaseCuenta> ClasesPorMiembro(IEnumerable<AsignacionCuenta> asignaciones) =>
        asignaciones.ToDictionary(a => Generacion.GeneradorModeloBpc.IdCuenta(a.PlanCuentas, a.Cuenta), a => Clase(a.Rubro))
            .Concat(CuentasTecnicas.Select(t => KeyValuePair.Create(t.Id, Clase(t.Rubro))))
            .ToDictionary(k => k.Key, k => k.Value);
}
