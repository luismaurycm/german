using System.Globalization;
using Acelerador.Core.Dominio;
using Microsoft.AspNetCore.Html;

namespace Acelerador.Web;

/// <summary>Íconos del prototipo F4. Siempre acompañan un texto: el estado nunca depende solo del color o del ícono (WCAG 1.4.1).</summary>
public static class Iconos
{
    public static readonly HtmlString Ok = new("""<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true" focusable="false"><circle cx="10" cy="10" r="9" fill="#1E7B4F"/><path d="M6 10.5l2.5 2.5L14 7.5" stroke="#fff" stroke-width="2" fill="none" stroke-linecap="round" stroke-linejoin="round"/></svg>""");
    public static readonly HtmlString Actual = new("""<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true" focusable="false"><circle cx="10" cy="10" r="8" fill="none" stroke="#144E8C" stroke-width="2"/><circle cx="10" cy="10" r="4" fill="#144E8C"/></svg>""");
    public static readonly HtmlString Pendiente = new("""<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true" focusable="false"><circle cx="10" cy="10" r="8" fill="none" stroke="#7F8A9B" stroke-width="2"/></svg>""");
    public static readonly HtmlString Error = new("""<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true" focusable="false"><circle cx="10" cy="10" r="9" fill="#B3261E"/><path d="M10 5.5v5.5M10 14v.5" stroke="#fff" stroke-width="2.2" stroke-linecap="round"/></svg>""");
    public static readonly HtmlString Info = new("""<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true" focusable="false"><circle cx="10" cy="10" r="9" fill="none" stroke="#144E8C" stroke-width="1.8"/><path d="M10 9v5M10 6v.5" stroke="#144E8C" stroke-width="2" stroke-linecap="round"/></svg>""");
    public static readonly HtmlString Aviso = new("""<svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true" focusable="false"><path d="M10 2.5l8 14H2z" fill="#F2B707"/><path d="M10 8v4M10 14.2v.3" stroke="#1B2533" stroke-width="2" stroke-linecap="round"/></svg>""");
    public static readonly HtmlString SinProbar = new("""<svg width="14" height="14" viewBox="0 0 20 20" aria-hidden="true" focusable="false"><circle cx="10" cy="10" r="7" fill="none" stroke="#2C4A70" stroke-width="2.4"/></svg>""");
    public static readonly HtmlString Mas = new("""<svg width="18" height="18" viewBox="0 0 18 18" aria-hidden="true" focusable="false"><path d="M9 3v12M3 9h12" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>""");
    public static readonly HtmlString Vacio = new("""<svg width="48" height="48" viewBox="0 0 48 48" aria-hidden="true" focusable="false"><rect x="8" y="12" width="32" height="26" rx="4" fill="none" stroke="#9FB3CC" stroke-width="2"/><path d="M8 20h32" stroke="#9FB3CC" stroke-width="2"/></svg>""");
}

public static class Formato
{
    private static readonly string[] Meses = ["Ene", "Feb", "Mar", "Abr", "May", "Jun", "Jul", "Ago", "Sep", "Oct", "Nov", "Dic"];

    /// <summary>«16-Oct-26 10:42» en hora local, como en el prototipo.</summary>
    public static string Fecha(DateTimeOffset? fecha)
    {
        if (fecha is null)
        {
            return "—";
        }

        var f = fecha.Value.ToLocalTime();
        return string.Create(CultureInfo.InvariantCulture, $"{f.Day:00}-{Meses[f.Month - 1]}-{f.Year % 100:00} {f.Hour:00}:{f.Minute:00}");
    }

    public static string Ruta(Guid clienteId, string paso) => paso switch
    {
        Pasos.Conexiones => $"/clientes/{clienteId}/conexiones",
        Pasos.Modulos => $"/clientes/{clienteId}/modulos",
        _ => $"/clientes/{clienteId}/paso/{paso}",
    };
}

/// <summary>Modelo de la navegación lateral de los pasos.</summary>
public sealed record ModeloNavegacion(Cliente Cliente, string PasoVisto);
