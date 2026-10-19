using System.Net;
using Acelerador.Tests.Soporte;
using static Acelerador.Tests.Web.PantallasSprint2Tests;
using static Acelerador.Tests.Web.PantallasSprint6Tests;

namespace Acelerador.Tests.Web;

/// <summary>UX-08, UX-09 y UX-13 con el modelo de propiedad, la eliminación de inversión y C02 (HU-32 a HU-35).</summary>
[Collection("Web")]
public class PantallasSprint7Tests
{
    private static async Task<(FabricaWeb App, Navegador Nav, string Cliente)> ConsolidadoAsync()
    {
        var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanCompletoAsync(app, nav);
        await GenerarAsync(nav, cliente);
        return (app, nav, cliente);
    }

    private static List<KeyValuePair<string, string>> Filas(string periodo, string accion, params (string Soc, string Ctrl, string Cons, string Prop, string Met)[] filas)
    {
        var lista = new List<KeyValuePair<string, string>> { new("periodo", periodo), new("accion", accion) };
        for (var i = 0; i < filas.Length; i++)
        {
            lista.Add(new($"filas[{i}].Sociedad", filas[i].Soc));
            lista.Add(new($"filas[{i}].Control", filas[i].Ctrl));
            lista.Add(new($"filas[{i}].Consolidacion", filas[i].Cons));
            lista.Add(new($"filas[{i}].Propiedad", filas[i].Prop));
            lista.Add(new($"filas[{i}].Metodo", filas[i].Met));
        }

        return lista;
    }

    [Fact(DisplayName = "HU-32 CA1 y CA2 y HU-33 CA4: UX-08 lista el modelo de propiedad, la plantilla C02 y la eliminación de inversión")]
    [Trait("HU", "HU-32")]
    [Trait("HU", "HU-33")]
    public async Task UX08Propiedad()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        await LlegarAPlanCompletoAsync(app, nav);
        var html = WebUtility.HtmlDecode(nav.Html);

        Assert.Contains("<h3>Modelo de propiedad</h3>", html, StringComparison.Ordinal);
        Assert.Contains(">PROPIEDAD</td>", html, StringComparison.Ordinal);
        Assert.Contains(">C02_Participaciones.xlsx</td>", html, StringComparison.Ordinal);
        Assert.Contains("<h3>Eliminación de inversión</h3>", html, StringComparison.Ordinal);
        Assert.Contains("Interés minoritario separado en ZACC_IM", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-33 CA4: UX-09 muestra el paso «Eliminación de inversión e interés minoritario»")]
    [Trait("HU", "HU-33")]
    public async Task UX09Inversion()
    {
        var (app, nav, _) = await ConsolidadoAsync();
        using var _a = app;

        Assert.Matches(@"\d+\. Eliminación de inversión e interés minoritario", nav.Texto);
        Assert.Contains("interés minoritario en ZACC_IM", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-35 CA1: UX-13 muestra la estructura inicial y guarda un cambio del periodo")]
    [Trait("HU", "HU-35")]
    public async Task UX13Guardar()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/participaciones?periodo=2026-09");
        Assert.Contains("<h1>C02 Participaciones y métodos</h1>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("name=\"filas[1].Propiedad\" value=\"80\"", nav.Html, StringComparison.Ordinal);

        await nav.PostAsync($"{cliente}/participaciones", Filas("2026-10", "guardar",
            ("1000", "100", "100", "100", "Matriz"), ("2000", "90", "100", "90", "IntegracionGlobal"), ("3000", "100", "100", "100", "IntegracionGlobal")));

        Assert.Equal($"{cliente}/participaciones?periodo=2026-10", nav.Ruta);
        Assert.Contains("Guardadas 3 filas en 2026-10.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("% propiedad 80 → 90", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"<td data-l=""Dato"" class=""mono"">POWN</td>\s*<td data-l=""Anterior"" class=""mono"">80</td>\s*<td data-l=""Nuevo"" class=""mono"">90</td>", nav.Html);
    }

    [Fact(DisplayName = "HU-35 CA2: «Copiar del periodo anterior» llena el periodo vacío con las del mes previo")]
    [Trait("HU", "HU-35")]
    public async Task UX13Copiar()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/participaciones?periodo=2026-10");
        Assert.Contains("Sin participaciones para este periodo", nav.Texto, StringComparison.Ordinal);

        await nav.PostAsync($"{cliente}/participaciones", Filas("2026-10", "copiar"));

        Assert.Contains("Copiadas las participaciones vigentes al periodo anterior (3 sociedades)", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("name=\"filas[1].Propiedad\" value=\"80\"", nav.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("Sin participaciones para este periodo", nav.Texto, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "HU-35 CA3: un porcentaje fuera de rango o sin método marca la celda y muestra «No se guardó la fila»")]
    [Trait("HU", "HU-35")]
    [InlineData("150", "IntegracionGlobal", "% control de 2000")]
    [InlineData("80", "", "Método de 2000")]
    public async Task UX13Error(string control, string metodo, string celda)
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/participaciones");
        await nav.PostAsync($"{cliente}/participaciones", Filas("2026-10", "guardar",
            ("1000", "100", "100", "100", "Matriz"), ("2000", control, "100", "80", metodo)));

        Assert.Contains("No se guardó la fila 2000.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Guardadas 1 fila en 2026-10.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"aria-label=\"{celda}\" aria-invalid=\"true\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("class=\"fila-err\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-35: si BPC no responde, UX-13 muestra el estado de error de conexión")]
    [Trait("HU", "HU-35")]
    public async Task UX13ErrorConexion()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;
        Simulador(app).FallaLogica.Add("C02 2026-10");
        await nav.GetAsync($"{cliente}/participaciones");

        await nav.PostAsync($"{cliente}/participaciones", Filas("2026-10", "guardar", ("1000", "100", "100", "100", "Matriz")));

        Assert.Contains("No se pudo conectar a BPC; revisa la conexión de EPM.", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-35: UX-13 tiene estado de carga y el enlace en la navegación")]
    [Trait("HU", "HU-35")]
    public async Task UX13CargaYNavegacion()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/participaciones");

        Assert.Contains("data-carga=\"Consultando el modelo de propiedad…\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("data-ocupado=\"Guardando…\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains($"href=\"{cliente}/participaciones\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", nav.Html, StringComparison.Ordinal);
    }
}
