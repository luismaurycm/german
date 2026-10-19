using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>
/// Lee y valida el archivo de una sociedad no SAP (HU-25). Formato provisional, por definir en la pregunta 5 del DAF:
/// texto delimitado (punto y coma, coma o tabulador) en UTF-8 con encabezado y las columnas sociedad, cuenta, periodo
/// (aaaa-mm), tercero, ceco, cebe e importe en moneda local.
/// </summary>
public static partial class LectorArchivoNoSap
{
    public const int MaximoErroresListados = 200;
    public const int MaximoFilas = 200_000;
    public const long TamanoMaximo = 20 * 1024 * 1024;
    public const string CuentaSinMapear = "cuenta sin mapear";

    public static readonly IReadOnlyList<string> Columnas = ["sociedad", "cuenta", "periodo", "tercero", "ceco", "cebe", "importe"];

    public static readonly IReadOnlyList<string> Titulos = ["Sociedad", "Cuenta", "Periodo", "Tercero", "CeCo", "CeBe", "Importe"];

    public static string MensajeNoEsDelGrupo(string codigo) =>
        $"{codigo} no es una sociedad no SAP registrada en Sociedades. Solo se cargan sociedades del grupo (RN-07).";

    /// <summary>Plantilla de ejemplo para descargar desde UX-07.</summary>
    public static string Plantilla(string sociedad) =>
        "sociedad;cuenta;periodo;tercero;ceco;cebe;importe\r\n" +
        $"{sociedad};11050501;2026-09;;CC100;PC10;12500000\r\n" +
        $"{sociedad};41200501;2026-09;;;PC10;-48900000,50\r\n";

    /// <param name="sociedad">Sociedad no SAP a la que se sube el archivo.</param>
    /// <param name="sociedadesNoSap">Sociedades no SAP registradas en UX-05 (RN-07).</param>
    /// <param name="cuentasMapeadas">Cuentas del plan de la sociedad que tienen rubro (las demás son «cuenta sin mapear»).</param>
    public static ArchivoNoSap Leer(byte[] contenido, string nombre, string sociedad, IReadOnlyCollection<string> sociedadesNoSap, IReadOnlySet<string> cuentasMapeadas)
    {
        var errores = new List<ErrorArchivo>();
        var total = 0;
        void Error(int fila, string columna, string motivo)
        {
            total++;
            if (errores.Count < MaximoErroresListados)
            {
                errores.Add(new ErrorArchivo(fila, columna, motivo));
            }
        }

        ArchivoNoSap Resultado(IReadOnlyList<FilaNoSap> filas) => new(sociedad, nombre, filas, errores, total);

        if (!sociedadesNoSap.Contains(sociedad))
        {
            Error(0, "sociedad", MensajeNoEsDelGrupo(sociedad));
            return Resultado([]);
        }

        string texto;
        try
        {
            texto = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(contenido).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            Error(0, "archivo", "El archivo no está en UTF-8. Guárdalo como «CSV UTF-8» y vuelve a subirlo.");
            return Resultado([]);
        }

        var lineas = texto.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        while (lineas.Count > 0 && lineas[^1].Length == 0)
        {
            lineas.RemoveAt(lineas.Count - 1);
        }

        if (lineas.Count < 2)
        {
            Error(0, "archivo", "El archivo no tiene filas de datos.");
            return Resultado([]);
        }

        if (lineas.Count - 1 > MaximoFilas)
        {
            Error(0, "archivo", $"El archivo tiene más de {MaximoFilas:N0} filas; divídelo por periodo.");
            return Resultado([]);
        }

        var separador = new[] { ';', '\t', ',' }.OrderByDescending(c => lineas[0].Count(x => x == c)).First();
        var encabezado = lineas[0].Split(separador).Select(Normalizar).ToList();
        var indices = Columnas.ToDictionary(c => c, c => encabezado.IndexOf(c));
        var faltan = indices.Where(i => i.Value < 0).Select(i => i.Key).ToList();
        if (faltan.Count > 0)
        {
            Error(1, string.Join(", ", faltan), $"Faltan columnas en el encabezado: {string.Join(", ", faltan)}. Se esperan {string.Join(", ", Columnas)}.");
            return Resultado([]);
        }

        var coma = separador != ',';
        var filas = new List<FilaNoSap>();
        var otrasSociedades = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var n = 1; n < lineas.Count; n++)
        {
            var linea = lineas[n];
            var fila = n + 1;
            if (linea.Trim().Length == 0)
            {
                continue;
            }

            var celdas = linea.Split(separador);
            if (celdas.Length < encabezado.Count)
            {
                Error(fila, "archivo", $"La fila tiene {celdas.Length} columnas; se esperan {encabezado.Count}.");
                continue;
            }

            string Celda(string columna) => celdas[indices[columna]].Trim().Trim('"').Trim();
            var soc = Celda("sociedad").ToUpperInvariant();
            var cuenta = Celda("cuenta");
            var periodo = Periodo(Celda("periodo"));
            var importeTexto = Celda("importe");
            var errorAntes = total;

            if (!string.Equals(soc, sociedad, StringComparison.OrdinalIgnoreCase))
            {
                Error(fila, "sociedad", sociedadesNoSap.Contains(soc)
                    ? $"La fila es de {soc}; este archivo es de {sociedad}."
                    : MensajeNoEsDelGrupo(soc.Length == 0 ? "(vacía)" : soc));
                otrasSociedades.Add(soc);
            }

            if (cuenta.Length == 0)
            {
                Error(fila, "cuenta", "La cuenta es obligatoria.");
            }
            else if (!cuentasMapeadas.Contains(cuenta))
            {
                Error(fila, "cuenta", $"{cuenta}: la cuenta no está en el mapeo ({CuentaSinMapear}). Corrígela en el archivo o mapéala en Mapeo de cuentas.");
            }

            if (periodo is null)
            {
                Error(fila, "periodo", $"«{Celda("periodo")}» no es un periodo válido; usa aaaa-mm (p. ej. 2026-09).");
            }

            if (!TryImporte(importeTexto, coma, out var importe))
            {
                Error(fila, "importe", $"«{importeTexto}» no es un importe numérico.");
            }

            if (total == errorAntes)
            {
                filas.Add(new FilaNoSap(fila, soc, cuenta, periodo!, Celda("tercero"), Celda("ceco"), Celda("cebe"), importe));
            }
        }

        return Resultado(filas);
    }

    private static string? Periodo(string texto)
    {
        var m = PeriodoValido().Match(texto);
        return m.Success ? $"{m.Groups[1].Value}-{m.Groups[2].Value}" : null;
    }

    private static bool TryImporte(string texto, bool comaDecimal, out decimal valor)
    {
        var t = texto.Replace(" ", "", StringComparison.Ordinal).Replace('−', '-');
        var cultura = comaDecimal && t.Contains(',', StringComparison.Ordinal) ? CultureInfo.GetCultureInfo("es-CO") : CultureInfo.InvariantCulture;
        return decimal.TryParse(t, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, cultura, out valor) && t.Length > 0;
    }

    private static string Normalizar(string columna) =>
        new string(columna.Trim().Trim('"').ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());

    [GeneratedRegex(@"^(\d{4})[-./](0[1-9]|1[0-2])$")]
    private static partial Regex PeriodoValido();
}
