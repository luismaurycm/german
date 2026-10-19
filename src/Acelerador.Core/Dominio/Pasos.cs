namespace Acelerador.Core.Dominio;

/// <summary>Un paso de la implementación en la navegación lateral (UX-02 a UX-10).</summary>
public sealed record Paso(string Codigo, string Nombre, bool Construido, string Historias);

/// <summary>Pasos de la implementación, en el orden del prototipo F4.</summary>
public static class Pasos
{
    public const string Clientes = "UX-01";
    public const string Conexiones = "UX-02";
    public const string Diagnostico = "UX-03";
    public const string Modulos = "UX-04";
    public const string Sociedades = "UX-05";
    public const string Mapeo = "UX-06";
    public const string NoSap = "UX-07";
    public const string Plan = "UX-08";
    public const string Ejecucion = "UX-09";
    public const string Reportes = "UX-10";

    public static readonly IReadOnlyList<Paso> Todos =
    [
        new(Conexiones, "Conexiones", true, "HU-03 a HU-06, HU-57"),
        new(Diagnostico, "Diagnóstico", true, "HU-07, HU-17"),
        new(Modulos, "Módulos", true, "HU-08, HU-12"),
        new(Sociedades, "Sociedades", true, "HU-09, HU-10"),
        new(Mapeo, "Mapeo de cuentas", true, "HU-11"),
        new(NoSap, "Sociedades no SAP", true, "HU-25"),
        new(Plan, "Plan de generación", true, "HU-12, HU-13, HU-16"),
        new(Ejecucion, "Ejecución", true, "HU-14, HU-54"),
        new(Reportes, "Reportes", false, "HU-32, HU-38, HU-45"),
    ];

    public static int Indice(string codigo)
    {
        for (var i = 0; i < Todos.Count; i++)
        {
            if (Todos[i].Codigo == codigo)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Pasos que ve el cliente en la navegación: UX-07 solo aparece si alguna sociedad no está en SAP (HU-09 CA5).
    /// </summary>
    public static IReadOnlyList<Paso> Visibles(Cliente cliente) =>
        Todos.Where(p => p.Codigo != NoSap || cliente.TieneSociedadesNoSap).ToList();

    /// <summary>Paso siguiente al mapeo de cuentas: UX-07 si hay sociedades no SAP, si no el plan de generación.</summary>
    public static string DespuesDelMapeo(Cliente cliente) => cliente.TieneSociedadesNoSap ? NoSap : Plan;

    public static Paso? Buscar(string codigo) => Todos.FirstOrDefault(p => p.Codigo == codigo);

    public static string Nombre(string codigo) => Buscar(codigo)?.Nombre ?? codigo;
}
