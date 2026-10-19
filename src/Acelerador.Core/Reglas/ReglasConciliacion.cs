using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>
/// Una pareja de C01 (RF-34): lo que registra la sociedad con la contraparte y lo que registra la contraparte con ella, en
/// las cuentas intercompañía de balance o de resultados. La diferencia es lo que la eliminación dejaría en ZACC_DIF_IC.
/// </summary>
public sealed record ParejaIntercompania(
    string Sociedad,
    string Contraparte,
    string Naturaleza,
    IReadOnlyList<string> CuentasSociedad,
    IReadOnlyList<string> CuentasContraparte,
    decimal RegistraSociedad,
    decimal RegistraContraparte)
{
    public decimal Diferencia => RegistraSociedad - RegistraContraparte;
}

/// <summary>HU-38: conciliación intercompañía por pareja de sociedades del grupo (RN-07), antes de eliminar.</summary>
public static class ReglasConciliacion
{
    /// <summary>Umbral provisional de C01 (DAF, pregunta 14; el del plan de pruebas F7), en la moneda del reporte.</summary>
    public const decimal UmbralProvisional = 1_000.00m;

    public const string Balance = "Balance";
    public const string Resultados = "Resultados";

    public static bool SuperaUmbral(ParejaIntercompania p, decimal umbral) => Math.Abs(p.Diferencia) > umbral;

    /// <summary>
    /// Arma las parejas con los datos importados en la moneda del reporte. Solo cuentan las sociedades del grupo (las que
    /// no están fuera del perímetro), sin la inversión en subsidiarias, que se elimina contra el patrimonio (HU-33).
    /// </summary>
    public static IReadOnlyList<ParejaIntercompania> Parejas(
        IEnumerable<RegistroBpc> registros,
        IReadOnlySet<string> grupo,
        IReadOnlyDictionary<string, ClaseCuenta> clases,
        IReadOnlySet<string> excluir,
        string moneda)
    {
        string Naturaleza(string cuenta) => clases.TryGetValue(cuenta, out var c) && ReglasRubros.EsResultados(c) ? Resultados : Balance;

        var partidas = registros
            .Where(r => r.Modelo == MotorConsolidacion.Modelo && r.Origen == MotorConsolidacion.OrigenCarga && r.Grupo.Length == 0 && r.Moneda == moneda
                        && r.Intco != MotorConsolidacion.IntcoNinguno && r.Intco.StartsWith("I_", StringComparison.Ordinal)
                        && r.Cuenta != ReglasRubros.CuentaDiferenciaIntercompania.Id && !excluir.Contains(r.Cuenta))
            .Select(r => (r.Entidad, Socio: r.Intco[2..], r.Cuenta, Naturaleza: Naturaleza(r.Cuenta), r.Importe))
            .Where(p => grupo.Contains(p.Entidad) && grupo.Contains(p.Socio) && p.Entidad != p.Socio)
            .ToList();

        return partidas
            .GroupBy(p => (A: string.CompareOrdinal(p.Entidad, p.Socio) < 0 ? p.Entidad : p.Socio, B: string.CompareOrdinal(p.Entidad, p.Socio) < 0 ? p.Socio : p.Entidad, p.Naturaleza))
            .Select(g =>
            {
                var deA = g.Where(p => p.Entidad == g.Key.A).ToList();
                var deB = g.Where(p => p.Entidad == g.Key.B).ToList();
                return new ParejaIntercompania(g.Key.A, g.Key.B, g.Key.Naturaleza,
                    deA.Select(p => p.Cuenta).Distinct().Order(StringComparer.Ordinal).ToList(),
                    deB.Select(p => p.Cuenta).Distinct().Order(StringComparer.Ordinal).ToList(),
                    deA.Sum(p => p.Importe), -deB.Sum(p => p.Importe));
            })
            .OrderBy(p => p.Sociedad, StringComparer.Ordinal).ThenBy(p => p.Contraparte, StringComparer.Ordinal).ThenBy(p => p.Naturaleza, StringComparer.Ordinal)
            .ToList();
    }
}
