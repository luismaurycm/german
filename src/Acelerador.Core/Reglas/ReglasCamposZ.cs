using System.Globalization;
using System.Text.RegularExpressions;

namespace Acelerador.Core.Reglas;

/// <summary>
/// HU-17 CA3: un campo Z se incorpora a la bodega como InfoObject de BW solo si su tipo de dato es compatible:
/// CHAR o NUMC de hasta 60 posiciones, DATS, TIMS, CUKY o UNIT. Textos largos (STRG, LCHR) y binarios (RAW, RSTR) no.
/// </summary>
public static partial class ReglasCamposZ
{
    public const int LongitudMaxima = 60;

    private static readonly string[] SinLongitud = ["DATS", "TIMS", "CUKY", "UNIT"];

    /// <summary>Null si el campo es compatible; si no, el motivo que se muestra en UX-03 y UX-08.</summary>
    public static string? MotivoIncompatible(string? tipoDato)
    {
        var t = (tipoDato ?? "").Trim().ToUpperInvariant();
        if (t.Length == 0)
        {
            return "Sin tipo de dato en el inventario: vuelve a escanear.";
        }

        var m = TipoConLongitud().Match(t);
        if (m.Success)
        {
            var longitud = int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture);
            return longitud <= LongitudMaxima
                ? null
                : $"Tipo {t} no compatible: un InfoObject admite hasta {LongitudMaxima} caracteres.";
        }

        return SinLongitud.Contains(t)
            ? null
            : $"Tipo {t} no compatible con un InfoObject de BW (solo CHAR, NUMC, DATS, TIMS, CUKY o UNIT).";
    }

    public static bool Compatible(string? tipoDato) => MotivoIncompatible(tipoDato) is null;

    /// <summary>Nombre del InfoObject en BW: el mismo nombre del campo Z (los campos ZZ ya están en el espacio del cliente).</summary>
    public static string InfoObject(string campo) => campo.Trim().ToUpperInvariant();

    [GeneratedRegex(@"^(CHAR|NUMC)\s*(\d+)$")]
    private static partial Regex TipoConLongitud();
}
