using System.Globalization;
using Acelerador.Core.Dominio;
using Acelerador.Sap;

namespace Acelerador.Core.Consolidacion;

/// <summary>
/// Modelo de propiedad de BPC (RF-31): las participaciones por sociedad y periodo se guardan como registros de las cuentas
/// PCTRL, PCON, POWN y METHOD. Un periodo sin datos usa el último periodo anterior registrado (RN-14).
/// </summary>
public static class ModeloPropiedad
{
    public const string Modelo = "PROPIEDAD";
    public const string Control = "PCTRL";
    public const string Consolidacion = "PCON";
    public const string Propiedad = "POWN";
    public const string Metodo = "METHOD";

    public static IReadOnlyList<RegistroBpc> Registros(string grupo, string periodoBpc, IEnumerable<Participacion> participaciones) =>
        participaciones.SelectMany(p => new[]
        {
            Registro(grupo, periodoBpc, p.Sociedad, Control, p.Control),
            Registro(grupo, periodoBpc, p.Sociedad, Consolidacion, p.Consolidacion),
            Registro(grupo, periodoBpc, p.Sociedad, Propiedad, p.Propiedad),
            Registro(grupo, periodoBpc, p.Sociedad, Metodo, (int)p.Metodo),
        }).ToList();

    /// <summary>Las participaciones registradas exactamente en el periodo (vacío si no hay: C02 muestra el estado vacío).</summary>
    public static IReadOnlyList<Participacion> DelPeriodo(IEnumerable<RegistroBpc> registros, string grupo, string periodoBpc) =>
        Leer(registros.Where(r => r.Modelo == Modelo && r.Grupo == grupo && r.Periodo == periodoBpc));

    /// <summary>RN-14: las participaciones vigentes en un periodo son las del último periodo registrado que no es posterior.</summary>
    public static (string? Periodo, IReadOnlyList<Participacion> Participaciones) Vigentes(IEnumerable<RegistroBpc> registros, string grupo, string periodoBpc)
    {
        var propias = registros.Where(r => r.Modelo == Modelo && r.Grupo == grupo).ToList();
        var periodo = propias.Select(r => r.Periodo).Where(p => string.CompareOrdinal(p, periodoBpc) <= 0).OrderDescending(StringComparer.Ordinal).FirstOrDefault();
        return periodo is null ? (null, []) : (periodo, Leer(propias.Where(r => r.Periodo == periodo)));
    }

    /// <summary>Periodo anterior en BPC: «2026.07» → «2026.06»; «2026.01» → «2025.12».</summary>
    public static string PeriodoAnterior(string periodoBpc)
    {
        var anio = int.Parse(periodoBpc[..4], CultureInfo.InvariantCulture);
        var mes = int.Parse(periodoBpc[5..], CultureInfo.InvariantCulture);
        return mes == 1 ? $"{anio - 1}.12" : $"{anio}.{mes - 1:00}";
    }

    private static IReadOnlyList<Participacion> Leer(IEnumerable<RegistroBpc> registros) =>
        registros.GroupBy(r => r.Entidad)
            .Select(g =>
            {
                decimal V(string cuenta) => g.FirstOrDefault(r => r.Cuenta == cuenta)?.Importe ?? 0m;
                return new Participacion(g.Key, V(Control), V(Consolidacion), V(Propiedad), (MetodoConsolidacion)(int)V(Metodo));
            })
            .OrderBy(p => p.Sociedad, StringComparer.Ordinal)
            .ToList();

    private static RegistroBpc Registro(string grupo, string periodo, string sociedad, string cuenta, decimal valor) =>
        new(Modelo, sociedad, cuenta, MotorConsolidacion.IntcoNinguno, periodo, "", "", MotorConsolidacion.OrigenCarga, valor, grupo);
}
