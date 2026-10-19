using System.Net;
using Acelerador.Core.Dominio;
using Acelerador.Tests.Soporte;
using static Acelerador.Tests.Web.PantallasSprint2Tests;
using static Acelerador.Tests.Web.PantallasSprint6Tests;

namespace Acelerador.Tests.Web;

/// <summary>UX-12 Captura de presupuesto y UX-11 con notas, presupuesto y reportes de BW (HU-46, HU-47, HU-49 a HU-53).</summary>
[Collection("Web")]
public class PantallasSprint10Tests
{
    private static async Task<(FabricaWeb App, Navegador Nav, string Cliente)> ConsolidadoAsync()
    {
        var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanCompletoAsync(app, nav, [Modulo.Consolidacion, Modulo.Presupuesto, Modulo.ActivosFijos, Modulo.Deterioros]);
        await GenerarAsync(nav, cliente);
        return (app, nav, cliente);
    }

    [Fact(DisplayName = "HU-49 CA3: UX-08 lista el modelo de presupuesto y UX-09 muestra el paso «Modelo de presupuesto»")]
    [Trait("HU", "HU-49")]
    public async Task PlanYEjecucion()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanCompletoAsync(app, nav, [Modulo.Consolidacion, Modulo.Presupuesto]);
        Assert.Contains("<h3>Modelo de presupuesto</h3>", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains(">PLANEACION</td>", nav.Html, StringComparison.Ordinal);

        await GenerarAsync(nav, cliente);

        Assert.Matches(@"\d+\. Modelo de presupuesto", nav.Texto);
    }

    [Fact(DisplayName = "HU-50 CA1 y HU-51 CA1: UX-12 elige sociedad, versión y año, con celdas editables en amarillo y totales")]
    [Trait("HU", "HU-50")]
    [Trait("HU", "HU-51")]
    public async Task UX12()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/presupuesto?sociedad=1000");
        var html = WebUtility.HtmlDecode(nav.Html);

        Assert.Contains("<h1>P05 Captura de presupuesto</h1>", html, StringComparison.Ordinal);
        foreach (var x in new[] { "<label for=\"sociedad\">Sociedad</label>", "<label for=\"version\">Versión</label>", "<label for=\"anio\">Año</label>" })
        {
            Assert.Contains(x, html, StringComparison.Ordinal);
        }

        foreach (var v in VersionPresupuesto.Propuestas)
        {
            Assert.Contains($">{v.Nombre}</option>", html, StringComparison.Ordinal);
        }

        Assert.Contains("class=\"ent num corto celda-epm\" name=\"filas[0].Meses[0]\"", html, StringComparison.Ordinal);
        Assert.Contains("data-total-general aria-live=\"polite\"", html, StringComparison.Ordinal);
        Assert.Contains("Resultado (ingresos − gastos)", html, StringComparison.Ordinal);
        Assert.Contains("Modelo PLANEACION · Presupuesto anual 2026", html, StringComparison.Ordinal);
        Assert.Contains("data-ocupado=\"Guardando…\"", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-50 CA2, CA3 y CA4: una celda que no es número se marca en rojo y no se guarda; las válidas se guardan; versión vacía")]
    [Trait("HU", "HU-50")]
    public async Task UX12Guardar()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;
        await nav.GetAsync($"{cliente}/presupuesto?sociedad=1000&version=PROY_6_6&anio=2026");
        Assert.Contains("Sin presupuesto para esta versión: ingresa los valores.", nav.Texto, StringComparison.Ordinal);
        var cuenta = System.Text.RegularExpressions.Regex.Match(nav.Html, @"name=""filas\[0\]\.Cuenta"" value=""([^""]+)""").Groups[1].Value;
        var centro = System.Text.RegularExpressions.Regex.Match(nav.Html, @"name=""filas\[0\]\.Centro"" value=""([^""]+)""").Groups[1].Value;
        List<KeyValuePair<string, string>> Campos(string mes1) =>
        [
            new("sociedad", "1000"), new("version", "PROY_6_6"), new("anio", "2026"),
            new("filas[0].Cuenta", cuenta), new("filas[0].Centro", centro), new("filas[0].Meses[0]", mes1), new("filas[0].Meses[1]", "2.500"),
        ];

        await nav.PostAsync($"{cliente}/presupuesto", Campos("abc"));

        Assert.Contains("1 celda no se guardó.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("name=\"filas[0].Meses[0]\" value=\"abc\"", nav.Html, StringComparison.Ordinal);
        Assert.Matches(@"name=""filas\[0\]\.Meses\[0\]""[^>]*aria-invalid=""true""", nav.Html);
        Assert.Contains("name=\"filas[0].Meses[1]\" value=\"2.500,00\"", nav.Html, StringComparison.Ordinal);

        await nav.PostAsync($"{cliente}/presupuesto", Campos("1.000"));
        Assert.Equal($"{cliente}/presupuesto?sociedad=1000&version=PROY_6_6&anio=2026", nav.Ruta);
        Assert.Contains("Presupuesto guardado: Proyección 6+6 2026 de 1000", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-52 CA2 y HU-51 CA3: P01 en UX-11 muestra presupuesto, ejecución, variación y % con la versión en el encabezado")]
    [Trait("HU", "HU-52")]
    [Trait("HU", "HU-51")]
    public async Task UX11P01()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/P01?sociedad=1000&version=PROY_3_9&anio=2026");
        var html = WebUtility.HtmlDecode(nav.Html);

        Assert.Contains("<h1>P01 Presupuesto frente a ejecución</h1>", html, StringComparison.Ordinal);
        Assert.Contains("Modelo PLANEACION · Versión Proyección 3+9 2026", html, StringComparison.Ordinal);
        foreach (var col in new[] { "Presupuesto", "Ejecución", "Variación", "% ejecución" })
        {
            Assert.Contains($"<th scope=\"col\" class=\"num\">{col}</th>", html, StringComparison.Ordinal);
        }

        Assert.Contains("<option value=\"PROY_3_9\" selected=\"selected\">Proyección 3+9</option>", html, StringComparison.Ordinal);
        await nav.GetAsync($"{cliente}/reportes/P01?sociedad=1000");
        Assert.Matches(@"\d+,\d\d %", nav.Texto);
    }

    [Fact(DisplayName = "HU-53 CA1, CA2 y CA3: A01 y D01 leen BW, la fuente es BW en UX-10 y muestran saldo inicial, movimientos y saldo final")]
    [Trait("HU", "HU-53")]
    public async Task UX11Bw()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/A01?sociedad=1000&periodo=2026-09");
        var html = WebUtility.HtmlDecode(nav.Html);
        Assert.Contains("<span class=\"pill warn\">Fuente BW</span>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\" class=\"num\">Saldo inicial</th>", html, StringComparison.Ordinal);
        Assert.Contains("87.167.250.000,00", nav.Texto, StringComparison.Ordinal);
        await nav.GetAsync($"{cliente}/reportes/D01?sociedad=1000&periodo=2026-09");
        Assert.Contains("13050501 Clientes nacionales", nav.Texto, StringComparison.Ordinal);

        await nav.GetAsync($"{cliente}/reportes");
        Assert.Matches(@"<b>A01</b></a>\s*</td>\s*<td data-l=""Reporte"">[^<]+</td>\s*<td data-l=""Módulo"">Activos fijos</td>\s*<td data-l=""Fuente"">\s*<span class=""pill warn"">BW</span>", WebUtility.HtmlDecode(nav.Html));
        Assert.Contains($"<a href=\"{cliente}/presupuesto\"><b>P05</b></a>", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-46 CA2 y HU-47 CA2: R10 muestra los movimientos de BW y R20 las cifras en moneda local y dólares")]
    [Trait("HU", "HU-46")]
    [Trait("HU", "HU-47")]
    public async Task UX11Notas()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/R10?periodo=2026-09&sociedad=1000&filiales=No&moneda=COP");
        Assert.Contains("Movimientos de BW de 1000", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Construcciones y edificaciones", nav.Texto, StringComparison.Ordinal);
        await nav.GetAsync($"{cliente}/reportes/R20?periodo=2026-09");
        var html = WebUtility.HtmlDecode(nav.Html);
        Assert.Contains("<th scope=\"col\" class=\"num\">Moneda local (COP)</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\" class=\"num\">Dólares (USD)</th>", html, StringComparison.Ordinal);
    }
}
