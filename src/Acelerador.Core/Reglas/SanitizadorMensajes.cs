using System.Text.RegularExpressions;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>
/// Quita credenciales de los mensajes que devuelve SAP antes de mostrarlos o registrarlos
/// (HU-04 CA4, RNF-Seg-02). Reemplaza el secreto exacto y cualquier «password=valor», «PASSWD: valor» o «clave=valor».
/// </summary>
public static partial class SanitizadorMensajes
{
    [GeneratedRegex(@"(?i)\b(password|passwd|pwd|clave|contraseña)(\s*[:=]\s*)\S+")]
    private static partial Regex PatronClave();

    public static string Limpiar(string? mensaje, params CredencialSap?[] credenciales)
    {
        if (string.IsNullOrEmpty(mensaje))
        {
            return "";
        }

        var limpio = mensaje;
        foreach (var c in credenciales)
        {
            if (c is not null)
            {
                limpio = limpio.Replace(c.Revelar(), CredencialSap.Mascara, StringComparison.Ordinal);
            }
        }

        limpio = PatronClave().Replace(limpio, m => $"{m.Groups[1].Value}{m.Groups[2].Value}{CredencialSap.Mascara}");
        return limpio.Length > 500 ? limpio[..500] + "…" : limpio;
    }
}
