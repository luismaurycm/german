using System.IO.Compression;
using System.Net;
using Acelerador.Sap;
using Acelerador.Tests.Soporte;
using static Acelerador.Tests.Web.PantallasSprint2Tests;
using static Acelerador.Tests.Web.PantallasSprint6Tests;

namespace Acelerador.Tests.Web;

/// <summary>UX-10 Reportes y UX-11 con R01 a R07 (HU-42 a HU-45, HU-48).</summary>
[Collection("Web")]
public class PantallasSprint9Tests
{
    private static async Task<(FabricaWeb App, Navegador Nav, string Cliente)> ConsolidadoAsync()
    {
        var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanCompletoAsync(app, nav);
        await GenerarAsync(nav, cliente);
        return (app, nav, cliente);
    }

    [Fact(DisplayName = "HU-48 CA1, CA2 y CA4: UX-10 lista el catálogo con C01 a C04 enlazados a su vista previa, genera los elegidos y muestra el estado")]
    [Trait("HU", "HU-48")]
    public async Task UX10Catalogo()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes");
        var html = WebUtility.HtmlDecode(nav.Html);
        Assert.Contains("<h1>Reportes</h1>", html, StringComparison.Ordinal);
        Assert.Matches(@"<th scope=""col"">ID</th><th scope=""col"">Reporte</th><th scope=""col"">Módulo</th><th scope=""col"">Fuente</th><th scope=""col"">Ola</th><th scope=""col"">Estado</th>", html);
        Assert.Contains($"<a href=\"{cliente}/reportes/R01\"><b>R01</b></a>", html, StringComparison.Ordinal);
        Assert.Contains($"<a href=\"{cliente}/reportes/C01\"><b>C01</b></a>", html, StringComparison.Ordinal);
        Assert.Contains($"<a href=\"{cliente}/participaciones\"><b>C02</b></a>", html, StringComparison.Ordinal);
        Assert.Contains($"<a href=\"{cliente}/cierre\"><b>C04</b></a>", html, StringComparison.Ordinal);
        Assert.Contains("Pendiente (próxima entrega)", html, StringComparison.Ordinal);
        Assert.Contains("data-ocupado=\"Generando…\"", html, StringComparison.Ordinal);

        await nav.PostAsync($"{cliente}/reportes?handler=Generar", [new("ids", "R01"), new("ids", "R07")]);

        Assert.Equal($"{cliente}/reportes", nav.Ruta);
        Assert.Contains("2 reportes generados.", nav.Texto, StringComparison.Ordinal);
        Assert.Equal(2, (nav.Html.Length - nav.Html.Replace(" Generado</span>", "", StringComparison.Ordinal).Length) / " Generado</span>".Length);
    }

    [Fact(DisplayName = "HU-48 CA3: Descargar reportes (.zip) entrega los Excel de los módulos elegidos")]
    [Trait("HU", "HU-48")]
    public async Task UX10Descargar()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;
        await nav.GetAsync($"{cliente}/reportes");

        var r = await nav.PostAsync($"{cliente}/reportes?handler=Descargar", new Dictionary<string, string?>(), seguir: false);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("application/zip", r.Content.Headers.ContentType!.MediaType);
        Assert.EndsWith("_2026-09.zip", r.Content.Headers.ContentDisposition!.FileNameStar ?? r.Content.Headers.ContentDisposition.FileName!.Trim('"'), StringComparison.Ordinal);
        using var zip = new ZipArchive(await r.Content.ReadAsStreamAsync());
        Assert.Contains(zip.Entries, x => x.FullName == "R01_Estado_de_situacion_financiera.xlsx");
        Assert.Contains(zip.Entries, x => x.FullName == "C04_Estado_del_cierre_por_sociedad_y_paso.xlsx");
    }

    [Fact(DisplayName = "HU-48: antes de ejecutar la generación UX-10 muestra el estado vacío")]
    [Trait("HU", "HU-48")]
    public async Task UX10Vacio()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanCompletoAsync(app, nav);

        await nav.GetAsync($"{cliente}/reportes");

        Assert.Contains("Ejecuta la generación para habilitar los reportes.", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Descargar reportes (.zip)", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-42 CA1, CA2 y HU-44 CA1: UX-11 muestra R01 con Sociedad, Incluir filiales, Moneda y Periodo, una columna por sociedad y el total")]
    [Trait("HU", "HU-42")]
    [Trait("HU", "HU-44")]
    public async Task UX11R01()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/R01?periodo=2026-09");
        var html = WebUtility.HtmlDecode(nav.Html);

        Assert.Contains("<h1>R01 Estado de situación financiera</h1>", html, StringComparison.Ordinal);
        foreach (var p in new[] { "<label for=\"sociedad\">Sociedad</label>", "<label for=\"filiales\">Incluir filiales</label>", "<label for=\"moneda\">Moneda</label>", "<label for=\"periodo\">Periodo</label>" })
        {
            Assert.Contains(p, html, StringComparison.Ordinal);
        }

        Assert.Contains("<option value=\"USD\">Dólares (USD)</option>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\" class=\"num\">Total consolidado</th>", html, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\" class=\"num\">Eliminaciones y ajustes</th>", html, StringComparison.Ordinal);
        Assert.Contains("Total pasivo y patrimonio", html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{cliente}/reportes/R01?periodo=2026-09&sociedad=1000&filiales=Sí&moneda=COP&rubro=F-00-101#detalle\"", html, StringComparison.Ordinal);

        await nav.GetAsync($"{cliente}/reportes/R01?periodo=2026-09&sociedad=1000&filiales=No&moneda=USD");
        html = WebUtility.HtmlDecode(nav.Html);
        Assert.DoesNotContain("Total consolidado", html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"No\" selected=\"selected\">No</option>", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-43 CA3: el enlace del rubro abre el detalle con las cuentas que lo forman")]
    [Trait("HU", "HU-43")]
    public async Task UX11Detalle()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/R01?periodo=2026-09&sociedad=1000&filiales=Sí&moneda=COP&rubro=F-00-101");

        Assert.Contains("Cuentas de F-00-101 Efectivo y equivalentes de efectivo", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("PCCO_11050501", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Cuenta 11050501 del plan PCCO", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-42 CA4: UX-11 sin datos y sin conexión a BPC")]
    [Trait("HU", "HU-42")]
    public async Task UX11Estados()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/R02?periodo=2026-12");
        Assert.Contains("Sin datos para la sociedad y el periodo elegidos.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("data-carga=\"Actualizando…\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);

        Simulador(app).SistemasConFallo.Add(SistemaSap.Bpc);
        await nav.GetAsync($"{cliente}/reportes/R05?periodo=2026-09");
        Assert.Contains("No se pudo conectar a BPC; revisa la conexión de EPM.", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-44 CA3 y HU-45 CA1, CA2: R05 por pareja, R03 con interés minoritario y diferencia en conversión, R04 y R06 en UX-11")]
    [Trait("HU", "HU-44")]
    [Trait("HU", "HU-45")]
    public async Task UX11OlasUnoYDos()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/R05?periodo=2026-09");
        Assert.Contains("Pareja 1000 ↔ 2000", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        await nav.GetAsync($"{cliente}/reportes/R03?periodo=2026-09");
        Assert.Contains("Interés minoritario", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("Diferencia en conversión (otro resultado integral)", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        await nav.GetAsync($"{cliente}/reportes/R04?periodo=2026-09");
        Assert.Contains("Efectivo al cierre del periodo", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Error del reporte", nav.Texto, StringComparison.Ordinal);
        await nav.GetAsync($"{cliente}/reportes/R06?periodo=2026-09");
        Assert.Contains("Subsidiaria", nav.Texto, StringComparison.Ordinal);
    }
}
