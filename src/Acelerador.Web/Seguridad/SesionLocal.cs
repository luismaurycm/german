using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Acelerador.Web.Seguridad;

/// <summary>
/// Token aleatorio de la sesión, generado al iniciar la aplicación (ADR-05). No hay pantalla de inicio
/// de sesión: el acelerador abre el navegador con el token en la URL, lo cambia por una cookie HttpOnly
/// y SameSite=Strict, y rechaza cualquier petición sin ella. Otro proceso o sitio web del equipo no
/// conoce el token, así que no puede usar la aplicación aunque llegue a 127.0.0.1.
/// </summary>
public sealed class SesionLocal
{
    public const string NombreCookie = "acelerador_sesion";
    public const string ParametroToken = "t";

    private readonly byte[] _token;

    public SesionLocal()
        : this(RandomNumberGenerator.GetBytes(32))
    {
    }

    internal SesionLocal(byte[] token)
    {
        _token = token;
        Token = WebEncoders.Base64UrlEncode(token);
    }

    public string Token { get; }

    public bool EsValido(string? candidato)
    {
        if (string.IsNullOrEmpty(candidato) || candidato.Length > 128)
        {
            return false;
        }

        var bytes = Encoding.ASCII.GetBytes(candidato);
        var esperado = Encoding.ASCII.GetBytes(Token);
        return CryptographicOperations.FixedTimeEquals(bytes, esperado);
    }

    public string UrlDeInicio(string baseUrl) => $"{baseUrl.TrimEnd('/')}/?{ParametroToken}={Token}";
}
