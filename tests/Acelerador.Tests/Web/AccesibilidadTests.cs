using System.Net;
using Acelerador.Core.Dominio;
using Acelerador.Tests.Soporte;
using static Acelerador.Tests.Web.PantallasSprint2Tests;
using static Acelerador.Tests.Web.PantallasSprint6Tests;

namespace Acelerador.Tests.Web;

/// <summary>HU-56: WCAG 2.1 AA en las pantallas del configurador (RNF-Usa-01).</summary>
[Collection("Web")]
public class AccesibilidadTests
{
    private static readonly Modulo[] Todos = [Modulo.Consolidacion, Modulo.Presupuesto, Modulo.ActivosFijos, Modulo.Deterioros];

    private static async Task<List<Incumplimiento>> RevisarAsync(Navegador nav, IEnumerable<string> rutas)
    {
        var lista = new List<Incumplimiento>();
        foreach (var ruta in rutas)
        {
            var r = await nav.GetAsync(ruta);
            Assert.True(r.StatusCode == HttpStatusCode.OK, $"{ruta}: {r.StatusCode}");
            lista.AddRange(RevisionAccesibilidad.Revisar(ruta, nav.Html));
        }

        return lista;
    }

    [Fact(DisplayName = "HU-56 CA1: UX-01 a UX-10 y las vistas de UX-11 a UX-15 no tienen incumplimientos A ni AA en la revisión automática")]
    [Trait("HU", "HU-56")]
    public async Task SinIncumplimientos()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        var lista = await RevisarAsync(nav, ["/", "/?nuevo=true"]);
        var cliente = await LlegarAPlanCompletoAsync(app, nav, Todos);
        lista.AddRange(await RevisarAsync(nav, [$"{cliente}/plan"]));
        await GenerarAsync(nav, cliente);

        lista.AddRange(await RevisarAsync(nav,
        [
            "/", $"{cliente}/conexiones", $"{cliente}/diagnostico", $"{cliente}/modulos", $"{cliente}/sociedades", $"{cliente}/mapeo", $"{cliente}/plan",
            $"{cliente}/ejecucion", $"{cliente}/registro", $"{cliente}/reportes", $"{cliente}/reportes/R01?rubro=F-00-101", $"{cliente}/reportes/R04",
            $"{cliente}/reportes/R10", $"{cliente}/reportes/R20", $"{cliente}/reportes/C01", $"{cliente}/reportes/C03", $"{cliente}/reportes/P01",
            $"{cliente}/reportes/A01", $"{cliente}/reportes/D02", $"{cliente}/participaciones", $"{cliente}/cierre", $"{cliente}/asientos", $"{cliente}/presupuesto",
            $"{cliente}/reportes/R01?periodo=2026-12", "/error?codigo=404",
        ]));

        Assert.True(lista.Count == 0, string.Join(Environment.NewLine, lista.Distinct().Take(40)));
    }

    [Fact(DisplayName = "HU-56 CA1: UX-07 Sociedades no SAP no tiene incumplimientos A ni AA")]
    [Trait("HU", "HU-56")]
    public async Task NoSap()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await PantallasSprint4Tests.LlegarANoSapAsync(nav);

        var lista = await RevisarAsync(nav, [$"{cliente}/no-sap"]);

        Assert.True(lista.Count == 0, string.Join(Environment.NewLine, lista));
    }

    [Theory(DisplayName = "HU-56 CA3 y 1.4.3 (AA): los colores de texto de los estados y controles tienen contraste de al menos 4,5:1")]
    [Trait("HU", "HU-56")]
    [InlineData("#1B2533", "#F4F6F9", "texto sobre el fondo")]
    [InlineData("#4A5568", "#FFFFFF", "texto secundario")]
    [InlineData("#4A5568", "#F4F6F9", "texto secundario sobre el fondo")]
    [InlineData("#144E8C", "#FFFFFF", "enlaces")]
    [InlineData("#FFFFFF", "#144E8C", "botón principal y encabezado")]
    [InlineData("#D6E2F2", "#144E8C", "subtítulo del encabezado")]
    [InlineData("#17603D", "#E3F2EA", "estado Completo")]
    [InlineData("#7A5300", "#FDF3D6", "estado con aviso")]
    [InlineData("#9B1C14", "#FBE4E2", "estado con error")]
    [InlineData("#2C4A70", "#EEF2F8", "estado Pendiente")]
    [InlineData("#1B2533", "#F2B707", "insignia de arquitectura")]
    [InlineData("#365F91", "#F4F7FB", "secciones de los reportes")]
    public void Contraste(string texto, string fondo, string uso) =>
        Assert.True(RevisionAccesibilidad.Contraste(texto, fondo) >= 4.5, $"{uso}: {RevisionAccesibilidad.Contraste(texto, fondo):0.00}:1");

    [Fact(DisplayName = "HU-56 CA2: el foco es visible en todos los controles (2.4.7 AA)")]
    [Trait("HU", "HU-56")]
    public void FocoVisible()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !Directory.Exists(Path.Combine(raiz.FullName, "src")))
        {
            raiz = raiz.Parent;
        }

        var css = File.ReadAllText(Path.Combine(raiz!.FullName, "src", "Acelerador.Web", "wwwroot", "css", "acelerador.css"));
        Assert.Contains(":focus-visible { outline: 3px solid", css, StringComparison.Ordinal);
        Assert.DoesNotContain("outline: none", css, StringComparison.Ordinal);
        Assert.DoesNotContain("outline:0", css.Replace(" ", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-56: la revisión automática detecta los incumplimientos que cubre")]
    [Trait("HU", "HU-56")]
    public void LaRevisionDetecta()
    {
        const string malo = """
            <!doctype html><html><head><title></title></head><body>
            <main><h1>Uno</h1><h1>Dos</h1>
            <input name="sin-etiqueta"><label for="x">X</label><input id="x" name="x">
            <button type="button"><svg width="1"></svg></button><a href="/a"></a><a href="/b" tabindex="2">Bien</a>
            <img src="a.png"><table><tr><th>Sin scope</th></tr></table><span id="d"></span><span id="d"></span>
            <input name="z" aria-label="Z" aria-invalid="si"></main></body></html>
            """;

        var criterios = RevisionAccesibilidad.Revisar("prueba", malo).Select(i => i.Criterio).ToList();

        foreach (var c in new[] { "3.1.1 (A)", "2.4.2 (A)", "1.3.1 (A)", "2.4.1 (A)", "4.1.1 (A)", "3.3.2 (A) / 4.1.2 (A)", "4.1.2 (A)", "2.4.4 (A)", "1.1.1 (A)", "2.4.3 (A)" })
        {
            Assert.Contains(c, criterios);
        }

        Assert.Equal(1, criterios.Count(c => c == "3.3.2 (A) / 4.1.2 (A)"));
        Assert.Equal(2, criterios.Count(c => c == "1.1.1 (A)"));
    }
}
