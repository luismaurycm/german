using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace Acelerador.Tests.Soporte;

/// <summary>Un incumplimiento de WCAG 2.1 encontrado en una página: criterio, nivel y detalle.</summary>
public sealed record Incumplimiento(string Pagina, string Criterio, string Detalle)
{
    public override string ToString() => $"{Pagina} · {Criterio}: {Detalle}";
}

/// <summary>
/// Revisión automática de WCAG 2.1 A y AA sobre el HTML que entrega el acelerador (HU-56, RNF-Usa-01). Cubre lo que se
/// verifica sin navegador: idioma y título (3.1.1, 2.4.2), un h1 y el enlace para saltar al contenido (2.4.1, 1.3.1),
/// nombre accesible de controles, botones y enlaces (4.1.2, 2.4.4, 3.3.2), íconos decorativos ocultos (1.1.1), tablas con
/// título y encabezados con alcance (1.3.1), ids únicos (4.1.1), sin tabindex positivo (2.4.3) y estados ARIA válidos.
/// </summary>
public static partial class RevisionAccesibilidad
{
    private sealed record Etiqueta(bool Cierre, string Nombre, Dictionary<string, string?> Atributos, int Fin);


    public static IReadOnlyList<Incumplimiento> Revisar(string pagina, string html)
    {
        var lista = new List<Incumplimiento>();
        void Falla(string criterio, string detalle) => lista.Add(new Incumplimiento(pagina, criterio, detalle));

        var etiquetas = EtiquetaRegex().Matches(html).Select(m => new Etiqueta(m.Groups[1].Value == "/", m.Groups[2].Value.ToLowerInvariant(), Atributos(m.Groups[3].Value), m.Index + m.Length)).ToList();
        var posiciones = EtiquetaRegex().Matches(html).Select(m => m.Index).ToList();
        string? A(Etiqueta e, string n) => e.Atributos.TryGetValue(n, out var v) ? v : null;

        if (!LangRegex().IsMatch(html))
        {
            Falla("3.1.1 (A)", "<html> sin lang=\"es\"");
        }

        var titulo = TituloRegex().Match(html);
        if (!titulo.Success || string.IsNullOrWhiteSpace(titulo.Groups[1].Value))
        {
            Falla("2.4.2 (A)", "la página no tiene <title>");
        }

        var h1 = etiquetas.Count(e => !e.Cierre && e.Nombre == "h1");
        if (h1 != 1)
        {
            Falla("1.3.1 (A)", $"la página tiene {h1} encabezados h1");
        }

        if (!html.Contains("href=\"#contenido\"", StringComparison.Ordinal) || !html.Contains("id=\"contenido\"", StringComparison.Ordinal))
        {
            Falla("2.4.1 (A)", "falta el enlace «Saltar al contenido» o el destino #contenido");
        }

        var ids = etiquetas.Where(e => !e.Cierre).Select(e => A(e, "id")).OfType<string>().GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        foreach (var id in ids)
        {
            Falla("4.1.1 (A)", $"id duplicado «{id}»");
        }

        var paraLabels = etiquetas.Where(e => !e.Cierre && e.Nombre == "label").Select(e => A(e, "for")).OfType<string>().ToHashSet();
        var dentroDeLabel = 0;
        for (var i = 0; i < etiquetas.Count; i++)
        {
            var e = etiquetas[i];
            if (e.Nombre == "label")
            {
                dentroDeLabel += e.Cierre ? -1 : 1;
                continue;
            }

            if (e.Cierre)
            {
                continue;
            }

            var tabindex = A(e, "tabindex");
            if (tabindex is not null && int.TryParse(tabindex, CultureInfo.InvariantCulture, out var t) && t > 0)
            {
                Falla("2.4.3 (A)", $"<{e.Nombre}> con tabindex={t}");
            }

            var invalido = A(e, "aria-invalid");
            if (invalido is not null && invalido is not ("true" or "false"))
            {
                Falla("4.1.2 (A)", $"aria-invalid=\"{invalido}\" en <{e.Nombre}>");
            }

            var conNombre = !string.IsNullOrWhiteSpace(A(e, "aria-label")) || !string.IsNullOrWhiteSpace(A(e, "aria-labelledby")) || !string.IsNullOrWhiteSpace(A(e, "title"));
            switch (e.Nombre)
            {
                case "input" or "select" or "textarea":
                    var tipo = A(e, "type") ?? "text";
                    if (tipo is "hidden" or "submit" or "button" or "reset")
                    {
                        break;
                    }

                    if (!conNombre && dentroDeLabel == 0 && (A(e, "id") is not { } idc || !paraLabels.Contains(idc)))
                    {
                        Falla("3.3.2 (A) / 4.1.2 (A)", $"<{e.Nombre} name=\"{A(e, "name")}\"> sin etiqueta");
                    }

                    break;
                case "button" or "a" or "summary":
                    if (e.Nombre == "a" && A(e, "href") is null)
                    {
                        break;
                    }

                    if (!conNombre && string.IsNullOrWhiteSpace(Texto(html, etiquetas, posiciones, i)))
                    {
                        Falla(e.Nombre == "a" ? "2.4.4 (A)" : "4.1.2 (A)", $"<{e.Nombre}> sin nombre accesible ({A(e, "href") ?? A(e, "name") ?? A(e, "class")})");
                    }

                    break;
                case "svg":
                    if (A(e, "aria-hidden") != "true" && !(A(e, "role") == "img" && conNombre))
                    {
                        Falla("1.1.1 (A)", "<svg> visible sin texto alternativo");
                    }

                    break;
                case "img":
                    if (A(e, "alt") is null)
                    {
                        Falla("1.1.1 (A)", $"<img src=\"{A(e, "src")}\"> sin alt");
                    }

                    break;
                case "table":
                    var siguiente = etiquetas.Skip(i + 1).FirstOrDefault(x => !x.Cierre);
                    if (!conNombre && siguiente?.Nombre != "caption")
                    {
                        Falla("1.3.1 (A)", "<table> sin <caption> ni aria-label");
                    }

                    break;
                case "th":
                    if (A(e, "scope") is null)
                    {
                        Falla("1.3.1 (A)", "<th> sin scope");
                    }

                    break;
            }
        }

        return lista;
    }

    /// <summary>Texto de un elemento (hasta su cierre), sin el contenido de los íconos ocultos.</summary>
    private static string Texto(string html, List<Etiqueta> etiquetas, List<int> posiciones, int indice)
    {
        var nombre = etiquetas[indice].Nombre;
        var nivel = 0;
        for (var j = indice + 1; j < etiquetas.Count; j++)
        {
            var e = etiquetas[j];
            if (e.Nombre == nombre)
            {
                if (!e.Cierre)
                {
                    nivel++;
                }
                else if (nivel-- == 0)
                {
                    var interior = html[etiquetas[indice].Fin..posiciones[j]];
                    var sinSvg = SvgRegex().Replace(interior, "");
                    var conAlternativo = AltRegex().Replace(sinSvg, " $1 ");
                    return WebUtility.HtmlDecode(EtiquetaRegex().Replace(conAlternativo, " ")).Trim();
                }
            }
        }

        return "";
    }

    private static Dictionary<string, string?> Atributos(string texto) =>
        AtributoRegex().Matches(texto).GroupBy(m => m.Groups[1].Value.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => (string?)WebUtility.HtmlDecode(g.First().Groups[2].Success ? g.First().Groups[2].Value : g.First().Groups[3].Success ? g.First().Groups[3].Value : g.First().Groups[4].Value));

    /// <summary>Razón de contraste de WCAG 2.1 entre dos colores #RRGGBB (1.4.3 AA pide 4,5:1 para texto normal).</summary>
    public static double Contraste(string a, string b)
    {
        static double Luminancia(string hex)
        {
            double Canal(int c)
            {
                var s = c / 255.0;
                return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            var v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return (0.2126 * Canal((v >> 16) & 0xFF)) + (0.7152 * Canal((v >> 8) & 0xFF)) + (0.0722 * Canal(v & 0xFF));
        }

        var (l1, l2) = (Luminancia(a), Luminancia(b));
        return (Math.Max(l1, l2) + 0.05) / (Math.Min(l1, l2) + 0.05);
    }

    [GeneratedRegex(@"<(/?)([a-zA-Z][a-zA-Z0-9]*)((?:\s+[^\s=>/]+(?:\s*=\s*(?:""[^""]*""|'[^']*'|[^\s>]+))?)*)\s*/?>")]
    private static partial Regex EtiquetaRegex();

    [GeneratedRegex(@"([^\s=>/]+)(?:\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+)))?")]
    private static partial Regex AtributoRegex();

    [GeneratedRegex(@"<html[^>]*\blang=""es""")]
    private static partial Regex LangRegex();

    [GeneratedRegex(@"<title>([^<]*)</title>")]
    private static partial Regex TituloRegex();

    [GeneratedRegex(@"<svg[\s\S]*?</svg>")]
    private static partial Regex SvgRegex();

    [GeneratedRegex(@"<img[^>]*\balt=""([^""]*)""[^>]*>")]
    private static partial Regex AltRegex();
}
