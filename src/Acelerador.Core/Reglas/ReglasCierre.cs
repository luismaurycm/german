using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>Periodo y monedas de la consolidación que se genera.</summary>
public static class ReglasCierre
{
    /// <summary>La generación consolida el último mes cerrado: el anterior a la fecha de ejecución («2026-09» en octubre).</summary>
    public static string PeriodoInicial(DateTimeOffset fecha)
    {
        var m = new DateTime(fecha.Year, fecha.Month, 1).AddMonths(-1);
        return $"{m.Year:0000}-{m.Month:00}";
    }

    /// <summary>La sociedad con método Matriz (la primera si no hay).</summary>
    public static Sociedad Matriz(IReadOnlyList<Sociedad> sociedades) =>
        sociedades.FirstOrDefault(s => s.Metodo == MetodoConsolidacion.Matriz) ?? sociedades[0];

    /// <summary>RN-06: moneda local del grupo (la de la matriz) y dólares.</summary>
    public static IReadOnlyList<string> MonedasPresentacion(IReadOnlyList<Sociedad> sociedades) =>
        new[] { Matriz(sociedades).MonedaLocal, ReglasMonedas.Dolar }.Distinct().ToList();

    public static string Grupo(IReadOnlyList<Sociedad> sociedades) => $"G_{Matriz(sociedades).Codigo}";

    /// <summary>RN-07 y RN-13: participan las sociedades del grupo que no están fuera del perímetro.</summary>
    public static IReadOnlyList<Sociedad> Participantes(IReadOnlyList<Sociedad> sociedades) =>
        sociedades.Where(s => s.Metodo != MetodoConsolidacion.FueraDelPerimetro).ToList();
}
