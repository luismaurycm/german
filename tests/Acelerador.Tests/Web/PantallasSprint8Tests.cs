using System.Net;
using Acelerador.Sap;
using Acelerador.Tests.Soporte;
using static Acelerador.Tests.Web.PantallasSprint2Tests;
using static Acelerador.Tests.Web.PantallasSprint6Tests;

namespace Acelerador.Tests.Web;

/// <summary>UX-11 (C01, C03), UX-14 Asientos de ajuste y UX-15 Estado del cierre (HU-37 a HU-41).</summary>
[Collection("Web")]
public class PantallasSprint8Tests
{
    private static async Task<(FabricaWeb App, Navegador Nav, string Cliente)> ConsolidadoAsync(Action<Acelerador.Sap.Simulador.OpcionesSimulador>? antes = null)
    {
        var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanCompletoAsync(app, nav);
        antes?.Invoke(Simulador(app));
        await GenerarAsync(nav, cliente);
        return (app, nav, cliente);
    }

    private static List<KeyValuePair<string, string>> Asiento(string accion, params (string Soc, string Cuenta, string Debito, string Credito)[] lineas)
    {
        var lista = new List<KeyValuePair<string, string>> { new("periodo", "2026-09"), new("moneda", "COP"), new("descripcion", "Reclasificación"), new("accion", accion) };
        for (var i = 0; i < lineas.Length; i++)
        {
            lista.Add(new($"lineas[{i}].Sociedad", lineas[i].Soc));
            lista.Add(new($"lineas[{i}].Cuenta", lineas[i].Cuenta));
            lista.Add(new($"lineas[{i}].Intco", "I_NONE"));
            lista.Add(new($"lineas[{i}].Debito", lineas[i].Debito));
            lista.Add(new($"lineas[{i}].Credito", lineas[i].Credito));
            lista.Add(new($"lineas[{i}].Observacion", "Ajuste"));
        }

        return lista;
    }

    [Fact(DisplayName = "HU-37 CA1 y CA2: C03 en UX-11 muestra cada sociedad y marca «No importada» con texto e ícono")]
    [Trait("HU", "HU-37")]
    public async Task UX11C03()
    {
        var (app, nav, cliente) = await ConsolidadoAsync(o => o.AjustesSaldo.Add("2000:11050501:5000000"));
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/C03?periodo=2026-09");

        Assert.Contains("<h1>C03 Validación de cuadre</h1>", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("Activo − (pasivo + patrimonio)", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("5.000.000,00", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"<span class=""pill err""><svg[^>]*aria-hidden=""true""[\s\S]*?</svg> No importada</span>", nav.Html);
        Assert.Equal(2, CuentaApariciones(nav.Html, "Importada</span>"));
    }

    [Fact(DisplayName = "HU-37 CA3 y CA4: C03 sin importaciones muestra el estado vacío y sin BPC el error de conexión de EPM")]
    [Trait("HU", "HU-37")]
    public async Task UX11C03VacioYError()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/C03?periodo=2026-12");
        Assert.Contains("Aún no hay importaciones en el periodo elegido.", nav.Texto, StringComparison.Ordinal);

        Simulador(app).SistemasConFallo.Add(SistemaSap.Bpc);
        await nav.GetAsync($"{cliente}/reportes/C03?periodo=2026-09");
        Assert.Contains("No se pudo conectar a BPC; revisa la conexión de EPM.", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-38 CA1 y CA2: C01 muestra las parejas y marca «Supera el umbral» con texto e ícono")]
    [Trait("HU", "HU-38")]
    public async Task UX11C01()
    {
        var (app, nav, cliente) = await ConsolidadoAsync(o =>
        {
            o.AjustesSaldo.Add("2000:22050501:5000000:1000");
            o.AjustesSaldo.Add("2000:11050501:-5000000");
        });
        using var _a = app;

        await nav.GetAsync($"{cliente}/reportes/C01?periodo=2026-09&moneda=COP");

        Assert.Contains("<h1>C01 Conciliación intercompañía</h1>", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("PCCO_13050501 / PCCO_22050501", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"<span class=""pill err""><svg[\s\S]*?</svg> Supera el umbral</span>", nav.Html);
        Assert.Contains("1 pareja supera el umbral", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Conciliada", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("<option value=\"USD\">USD</option>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("data-carga=\"Actualizando…\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-39 CA1, CA2 y CA4: UX-14 calcula «Descuadrado», deshabilita Guardar y, cuadrado, guarda y lista el asiento")]
    [Trait("HU", "HU-39")]
    public async Task UX14()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/asientos?periodo=2026-09");
        Assert.Contains("Aún no hay líneas: usa Agregar línea.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Aún no hay asientos en el periodo.", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"value=""guardar""[^>]*disabled", nav.Html);

        await nav.PostAsync($"{cliente}/asientos", Asiento("calcular", ("1000", "PCCO_11050501", "1.000.000", ""), ("1000", "PCCO_13050501", "", "900.000")));
        Assert.Contains("Descuadrado: 100.000,00", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"value=""guardar""[^>]*disabled", nav.Html);

        await nav.PostAsync($"{cliente}/asientos", Asiento("agregar", ("1000", "PCCO_11050501", "1.000.000", ""), ("1000", "PCCO_13050501", "", "1.000.000")));
        Assert.Contains("name=\"lineas[2].Sociedad\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Cuadrado", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"value=""guardar""[^>]*disabled", nav.Html);

        await nav.PostAsync($"{cliente}/asientos", Asiento("guardar", ("1000", "PCCO_11050501", "1.000.000", ""), ("1000", "PCCO_13050501", "", "1.000.000")));
        Assert.Equal($"{cliente}/asientos?periodo=2026-09&moneda=COP", nav.Ruta);
        Assert.Contains("Asiento AJ-0001 guardado en 2026-09 (COP).", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"<td data-l=""Asiento"" class=""mono"">AJ-0001</td>\s*<td data-l=""Descripción"">Reclasificación</td>\s*<td data-l=""Usuario"">[^<]+</td>\s*<td data-l=""Fecha""", WebUtility.HtmlDecode(nav.Html));
        Assert.Contains("data-ocupado=\"Guardando…\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-39 CA2 y CA3: guardar un asiento que no cuadra o con una sociedad bloqueada muestra el error y marca la celda")]
    [Trait("HU", "HU-39")]
    [Trait("HU", "HU-40")]
    public async Task UX14Errores()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;
        await nav.GetAsync($"{cliente}/cierre?periodo=2026-09");
        await nav.PostAsync($"{cliente}/cierre?handler=Bloqueo", new Dictionary<string, string?> { ["periodo"] = "2026-09", ["sociedad"] = "2000", ["bloquear"] = "true" });

        await nav.GetAsync($"{cliente}/asientos?periodo=2026-09");
        Assert.Contains("2000 está bloqueada en 2026-09: sus líneas no se aceptan (RN-17).", nav.Texto, StringComparison.Ordinal);
        await nav.PostAsync($"{cliente}/asientos", Asiento("guardar", ("1000", "PCCO_11050501", "10", ""), ("2000", "PCCO_11050501", "", "9")));

        Assert.Contains("El asiento no cuadra. Los débitos deben ser iguales a los créditos para guardar (RN-15).", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("La sociedad 2000 está bloqueada en 2026-09: sus líneas no se aceptan hasta que se desbloquee (RN-17).", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Sociedad de la línea 2\" aria-invalid=\"true\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Sociedad de la línea 1\" aria-invalid=\"false\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-40 CA1 y HU-41 CA1, CA3: UX-15 muestra la matriz en orden, Bloquear o Desbloquear por sociedad y las hojas C02 y Asientos")]
    [Trait("HU", "HU-40")]
    [Trait("HU", "HU-41")]
    public async Task UX15()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/cierre?periodo=2026-09");
        Assert.Contains("<h1>C04 Estado del cierre</h1>", nav.Html, StringComparison.Ordinal);
        Assert.Matches(@"<th scope=""col"">Carga</th>\s*<th scope=""col"">Cuadre</th>\s*<th scope=""col"">Conciliación IC</th>\s*<th scope=""col"">Conversión</th>\s*<th scope=""col"">Consolidación</th>\s*<th scope=""col"">Reportes</th>", WebUtility.HtmlDecode(nav.Html));
        Assert.Equal(3, CuentaApariciones(nav.Html, ">Bloquear</button>"));
        Assert.Contains($"href=\"{cliente}/participaciones?periodo=2026-09\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{cliente}/asientos?periodo=2026-09\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("data-carga=\"Consultando el estado del cierre…\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);

        await nav.PostAsync($"{cliente}/cierre?handler=Bloqueo", new Dictionary<string, string?> { ["periodo"] = "2026-09", ["sociedad"] = "2000", ["bloquear"] = "true" });

        Assert.Equal($"{cliente}/cierre?periodo=2026-09", nav.Ruta);
        Assert.Contains("2000 quedó bloqueada en 2026-09: no admite cargas, asientos ni reprocesos del periodo (RN-17).", nav.Texto, StringComparison.Ordinal);
        Assert.Equal(1, CuentaApariciones(nav.Html, ">Desbloquear</button>"));
        Assert.Equal(2, CuentaApariciones(nav.Html, ">Bloquear</button>"));
    }

    [Fact(DisplayName = "HU-41 CA2 y estado de error: el cuadre con error enlaza a C03 y se avisa qué sociedades no cuadran")]
    [Trait("HU", "HU-41")]
    public async Task UX15Error()
    {
        var (app, nav, cliente) = await ConsolidadoAsync(o => o.AjustesSaldo.Add("2000:11050501:5000000"));
        using var _a = app;

        await nav.GetAsync($"{cliente}/cierre?periodo=2026-09");

        Assert.Contains("1 sociedad no cuadra. 2000 no se importó; revisa C03 Validación de cuadre antes de consolidar.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"<a class=\"pill err\" href=\"{cliente}/reportes/C03?periodo=2026-09\">", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-41 CA4: UX-15 en un periodo sin cargas dice «El cierre de 2026-12 aún no ha iniciado»")]
    [Trait("HU", "HU-41")]
    public async Task UX15NoIniciado()
    {
        var (app, nav, cliente) = await ConsolidadoAsync();
        using var _a = app;

        await nav.GetAsync($"{cliente}/cierre?periodo=2026-12");

        Assert.Contains("El cierre de 2026-12 aún no ha iniciado.", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(">Bloquear</button>", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-37 a HU-41: la navegación lleva a C01, C03, C04 y Asientos, y UX-08 lista los controles del cierre")]
    [Trait("HU", "HU-41")]
    [Trait("HU", "HU-39")]
    public async Task NavegacionYPlan()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        await LlegarAPlanCompletoAsync(app, nav);
        var html = WebUtility.HtmlDecode(nav.Html);
        Assert.Contains("<h3>Controles del cierre</h3>", html, StringComparison.Ordinal);
        Assert.Contains(">ZACC_ESTADOS</td>", html, StringComparison.Ordinal);
        Assert.Contains(">C04_Estado_del_cierre.xlsx</td>", html, StringComparison.Ordinal);
        Assert.Contains("Bloquear una sociedad no bloquea a las demás", nav.Texto, StringComparison.Ordinal);

        var cliente = nav.Ruta[..nav.Ruta.LastIndexOf('/')];
        await GenerarAsync(nav, cliente);
        await nav.GetAsync($"{cliente}/cierre");
        foreach (var destino in new[] { "reportes/C01", "reportes/C03", "cierre", "asientos", "participaciones" })
        {
            Assert.Contains($"href=\"{cliente}/{destino}\"", nav.Html, StringComparison.Ordinal);
        }
    }

    private static int CuentaApariciones(string texto, string valor) => (texto.Length - texto.Replace(valor, "", StringComparison.Ordinal).Length) / valor.Length;
}
