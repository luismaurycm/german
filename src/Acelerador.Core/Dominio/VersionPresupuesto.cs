namespace Acelerador.Core.Dominio;

/// <summary>
/// Versión del modelo de planeación (RF-39, HU-12). <see cref="Codigo"/> será el miembro de la dimensión de versión
/// en BPC; <see cref="Anual"/> marca la versión de presupuesto anual, que no puede faltar.
/// </summary>
public sealed record VersionPresupuesto(string Codigo, string Nombre, bool Anual = false)
{
    /// <summary>Versiones propuestas al marcar Presupuesto: presupuesto anual y proyecciones 3+9, 6+6 y 9+3.</summary>
    public static readonly IReadOnlyList<VersionPresupuesto> Propuestas =
    [
        new("PRESUP_ANUAL", "Presupuesto anual", Anual: true),
        new("PROY_3_9", "Proyección 3+9"),
        new("PROY_6_6", "Proyección 6+6"),
        new("PROY_9_3", "Proyección 9+3"),
    ];
}
