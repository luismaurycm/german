using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>Datos que el consultor escribe en una tarjeta de UX-02.</summary>
public sealed record DatosConexion(string? Host, string? Mandante, string? UsuarioTecnico, string? CredencialNueva);

/// <summary>Reglas de HU-03 CA1: host, mandante, usuario técnico y credencial son obligatorios.</summary>
public static class ReglasConexion
{
    public const string CampoHost = "Host";
    public const string CampoMandante = "Mandante";
    public const string CampoUsuario = "UsuarioTecnico";
    public const string CampoCredencial = "Credencial";

    /// <summary>Errores por campo. Vacío si los datos son válidos.</summary>
    public static IReadOnlyDictionary<string, string> Validar(DatosConexion datos, bool tieneCredencialGuardada)
    {
        var errores = new Dictionary<string, string>();

        var host = (datos.Host ?? "").Trim();
        if (host.Length == 0)
        {
            errores[CampoHost] = "El host es obligatorio.";
        }
        else if (host.Any(char.IsWhiteSpace) || host.Length > 255)
        {
            errores[CampoHost] = "El host no puede tener espacios.";
        }

        var mandante = (datos.Mandante ?? "").Trim();
        if (mandante.Length == 0)
        {
            errores[CampoMandante] = "El mandante es obligatorio.";
        }
        else if (mandante.Length != 3 || !mandante.All(char.IsAsciiDigit))
        {
            errores[CampoMandante] = "El mandante tiene 3 dígitos, por ejemplo 100.";
        }

        var usuario = (datos.UsuarioTecnico ?? "").Trim();
        if (usuario.Length == 0)
        {
            errores[CampoUsuario] = "El usuario técnico es obligatorio.";
        }
        else if (usuario.Length > 12 || usuario.Any(char.IsWhiteSpace))
        {
            errores[CampoUsuario] = "El usuario de SAP tiene máximo 12 caracteres, sin espacios.";
        }

        if (string.IsNullOrEmpty(datos.CredencialNueva) && !tieneCredencialGuardada)
        {
            errores[CampoCredencial] = "La credencial es obligatoria.";
        }

        return errores;
    }

    /// <summary>Indica si los parámetros cambiaron respecto a lo guardado; si cambian, la prueba anterior ya no vale.</summary>
    public static bool Cambio(Conexion actual, DatosConexion datos) =>
        !string.Equals(actual.Host, (datos.Host ?? "").Trim(), StringComparison.OrdinalIgnoreCase)
        || actual.Mandante != (datos.Mandante ?? "").Trim()
        || !string.Equals(actual.UsuarioTecnico, (datos.UsuarioTecnico ?? "").Trim().ToUpperInvariant(), StringComparison.Ordinal)
        || !string.IsNullOrEmpty(datos.CredencialNueva);
}
