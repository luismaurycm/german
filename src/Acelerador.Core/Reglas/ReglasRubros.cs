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

    public const string RubroInversiones = "F-00-107";

    /// <summary>HU-33 CA3: el interés minoritario se presenta separado en el patrimonio consolidado.</summary>
    public static readonly CuentaTecnica CuentaInteresMinoritario = new("ZACC_IM", "Interés minoritario", "F-00-304");

    /// <summary>La parte del resultado del ejercicio que corresponde al interés minoritario (resta del resultado del grupo).</summary>
    public static readonly CuentaTecnica CuentaResultadoMinoritario = new("ZACC_RES_IM", "Resultado atribuible al interés minoritario", RubroResultado);

    /// <summary>HU-33 CA5: diferencia entre la inversión y el patrimonio eliminado, en una cuenta identificable.</summary>
    public static readonly CuentaTecnica CuentaDiferenciaInversion = new("ZACC_DIF_INV", "Diferencia en la eliminación de inversión", "F-00-108");

    /// <summary>HU-34: participación en el resultado de las coligadas (estado de resultados).</summary>
    public static readonly CuentaTecnica CuentaParticipacionResultado = new("ZACC_RES_PART", "Participación en resultados de asociadas", "F-00-402");

    /// <summary>Contrapartida en el resultado del ejercicio del balance de la participación en coligadas.</summary>
    public static readonly CuentaTecnica CuentaParticipacionEjercicio = new("ZACC_RES_PART_EJ", "Resultado del ejercicio por participación en asociadas", RubroResultado);

    public static readonly IReadOnlyList<CuentaTecnica> CuentasTecnicas =
    [
        CuentaDiferenciaConversion, CuentaDiferenciaIntercompania, CuentaInteresMinoritario, CuentaResultadoMinoritario,
        CuentaDiferenciaInversion, CuentaParticipacionResultado, CuentaParticipacionEjercicio,
    ];

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
