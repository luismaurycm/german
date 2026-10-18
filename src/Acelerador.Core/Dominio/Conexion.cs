using Acelerador.Sap;

namespace Acelerador.Core.Dominio;

public enum EstadoPrueba
{
    SinProbar,
    Exitosa,
    Fallida,
}

/// <summary>
/// Conexión a un sistema SAP del cliente. Guarda solo la referencia a la credencial:
/// el secreto vive en <see cref="Puertos.ICredentialStore"/> (ADR-04, RNF-Seg-02).
/// </summary>
public sealed class Conexion
{
    public required Guid ClienteId { get; init; }

    public required SistemaSap Sistema { get; init; }

    public string Host { get; set; } = "";

    public string Mandante { get; set; } = "";

    public string UsuarioTecnico { get; set; } = "";

    public string? ReferenciaCredencial { get; set; }

    public EstadoPrueba Estado { get; set; } = EstadoPrueba.SinProbar;

    /// <summary>Mensaje devuelto por SAP en la última prueba, ya sanitizado.</summary>
    public string? Mensaje { get; set; }

    public DateTimeOffset? FechaPrueba { get; set; }

    /// <summary>SID que respondió en la última prueba exitosa; lo usa RN-01.</summary>
    public string? Sid { get; set; }

    /// <summary>Producto que respondió (p. ej. «SAP BW 7.5»).</summary>
    public string? Producto { get; set; }

    public bool TieneCredencial => ReferenciaCredencial is not null;

    public ParametrosConexion Parametros => new(Host, Mandante, UsuarioTecnico);

    public static Conexion Nueva(Guid clienteId, SistemaSap sistema) => new() { ClienteId = clienteId, Sistema = sistema };

    public void ReiniciarPrueba()
    {
        Estado = EstadoPrueba.SinProbar;
        Mensaje = null;
        FechaPrueba = null;
        Sid = null;
        Producto = null;
    }
}
