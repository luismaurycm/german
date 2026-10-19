namespace Acelerador.Core.Reglas;

/// <summary>RN-06 y RN-11: la consolidación se presenta en moneda local y en dólares, con tasa por tipo de cuenta.</summary>
public static class ReglasMonedas
{
    public const string Dolar = "USD";

    /// <summary>RN-11 (por confirmar en la pregunta 3 del DAF): cierre para balance, promedio para resultados e histórica para patrimonio.</summary>
    /// <summary>Tipos de tasa de TCURR para cada tasa de la consolidación (por confirmar con el cliente, pregunta 3 del DAF).</summary>
    public static readonly IReadOnlyDictionary<string, string> TiposTcurr = new Dictionary<string, string>
    {
        ["cierre"] = "B",
        ["promedio"] = "M",
        ["historica"] = "H",
    };

    /// <summary>Periodos cuyas tasas se cargan: de enero del año en curso al mes anterior (o diciembre si es enero).</summary>
    public static IReadOnlyList<string> PeriodosCarga(DateTimeOffset ahora)
    {
        var fin = new DateTime(ahora.Year, ahora.Month, 1).AddMonths(-1);
        var inicio = new DateTime(fin.Year, 1, 1);
        var lista = new List<string>();
        for (var m = inicio; m <= fin; m = m.AddMonths(1))
        {
            lista.Add($"{m.Year:0000}-{m.Month:00}");
        }

        return lista;
    }

    public static readonly IReadOnlyList<(string Tipo, string Aplica)> TiposDeTasa =
    [
        ("Cierre", "Cuentas de balance (activo y pasivo)"),
        ("Promedio", "Cuentas de resultados"),
        ("Histórica", "Cuentas de patrimonio"),
    ];
}
