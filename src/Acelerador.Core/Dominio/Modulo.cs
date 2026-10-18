namespace Acelerador.Core.Dominio;

/// <summary>Módulos que se pueden implementar en un cliente (RF-05).</summary>
public enum Modulo
{
    Consolidacion,
    Presupuesto,
    ActivosFijos,
    Deterioros,
}

/// <summary>Ficha de un módulo en UX-04: qué genera, cuántos reportes y si crea modelo en BPC.</summary>
public sealed record FichaModulo(Modulo Modulo, string Nombre, string Genera, int Reportes, bool CreaModeloBpc)
{
    /// <summary>Nota de RN-12 para los módulos que se quedan en BW (HU-08 CA4).</summary>
    public string? Nota => CreaModeloBpc ? null : CatalogoModulos.NotaSoloBw;
}

/// <summary>Catálogo de módulos con los textos del prototipo F4 (34 reportes en total).</summary>
public static class CatalogoModulos
{
    public const string NotaSoloBw = "Se queda en BW; no crea modelo en BPC.";

    public static readonly IReadOnlyList<FichaModulo> Todos =
    [
        new(Modulo.Consolidacion, "Consolidación",
            "Bodega BW + modelo BPC de consolidación y de propiedad + 20 reportes + 4 de control (C01 a C04)", 24, true),
        new(Modulo.Presupuesto, "Presupuesto",
            "Carga desde 0FI_GL_14 + modelo de planeación BPC + 5 reportes · Versiones: presupuesto anual, 3+9, 6+6 y 9+3 (editables)", 5, true),
        new(Modulo.ActivosFijos, "Activos fijos", "Carga en BW + 3 reportes", 3, false),
        new(Modulo.Deterioros, "Deterioros", "Carga en BW + 2 reportes", 2, false),
    ];

    public static FichaModulo Ficha(Modulo modulo) => Todos.First(f => f.Modulo == modulo);
}
