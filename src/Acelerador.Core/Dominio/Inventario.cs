using Acelerador.Sap;

namespace Acelerador.Core.Dominio;

/// <summary>Un paso del escaneo de inventario: qué se lee y de qué sistema (HU-07 CA4 muestra el paso que falló).</summary>
public sealed record PasoInventario(TipoInventario Tipo, SistemaSap Sistema, string Nombre);

/// <summary>El escaneo no terminó: paso que falló, mensaje del sistema (sanitizado) y cuándo.</summary>
public sealed record FalloEscaneo(TipoInventario Tipo, string Mensaje, DateTimeOffset Fecha);

/// <summary>
/// Lo que ya existe en el cliente según el último escaneo exitoso (RF-03), los campos Z que el consultor eligió
/// incorporar a la bodega (RF-09) y, si el último intento falló, el fallo.
/// </summary>
public sealed class Inventario
{
    public required Guid ClienteId { get; init; }

    /// <summary>Fecha del último escaneo exitoso; null si nunca terminó uno.</summary>
    public DateTimeOffset? Fecha { get; set; }

    public string? Usuario { get; set; }

    public Dictionary<TipoInventario, IReadOnlyList<ObjetoInventario>> Objetos { get; set; } = [];

    public ISet<string> CamposZIncorporados { get; set; } = new HashSet<string>(StringComparer.Ordinal);

    public FalloEscaneo? Fallo { get; set; }

    public bool Escaneado => Fecha is not null;

    /// <summary>RN-03: BW ya tiene la bodega financiera si algún InfoProvider es de la bodega.</summary>
    public bool BodegaExistente => Lista(TipoInventario.InfoProviders).Any(o => o.EsBodegaFinanciera);

    public IReadOnlyList<ObjetoInventario> Lista(TipoInventario tipo) => Objetos.GetValueOrDefault(tipo) ?? [];
}
