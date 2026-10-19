using System.Net;
using System.Text.RegularExpressions;
using Acelerador.Tests.Soporte;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Acelerador.Tests.Web.PantallasSprint2Tests;

namespace Acelerador.Tests.Web;

/// <summary>Los pasos de datos maestros y del modelo BPC en UX-08 y UX-09 (HU-21 a HU-24, HU-26).</summary>
[Collection("Web")]
public class PantallasSprint5Tests
{
    private static readonly Dictionary<string, string?> Nada = [];

    /// <summary>Reloj fijo en octubre de 2026: las tasas se cargan de 2026-01 a 2026-09.</summary>
    private static FabricaWeb App() => new(Acelerador.Sap.Simulador.PerfilSimulado.ABw75,
        s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(new RelojFijo(new DateTimeOffset(2026, 10, 16, 15, 0, 0, TimeSpan.Zero)))));

    private static async Task<string> LlegarAPlanAsync(Navegador nav)
    {
        var cliente = await LlegarAMapeoAsync(nav, Entorno.FilasPrototipo().Take(3));
        await nav.PostAsync($"{cliente}/mapeo", [new("sociedad", "1000"), new("rubro", "F-00-101"), new("cuentas", "11050501"), new("accion", "asignar")]);
        await nav.PostAsync($"{cliente}/mapeo?handler=Continuar", Nada);
        return cliente;
    }

    private static async Task GenerarAsync(Navegador nav, string cliente)
    {
        await nav.GetAsync($"{cliente}/plan?confirmar=true");
        await nav.PostAsync($"{cliente}/plan?handler=Generar", Nada);
        for (var i = 0; i < 100 && nav.Html.Contains("data-refrescar=\"/", StringComparison.Ordinal); i++)
        {
            await Task.Delay(100);
            await nav.GetAsync($"{cliente}/ejecucion");
        }
    }

    [Fact(DisplayName = "HU-26 CA3 y HU-23 CA4: UX-08 lista el modelo de consolidación y las cargas; Monedas y tasas muestra moneda local y tipos de tasa")]
    [Trait("HU", "HU-26")]
    [Trait("HU", "HU-23")]
    public async Task UX08ModeloYMaestros()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        await LlegarAPlanAsync(nav);

        Assert.Contains("<h3>Modelo de consolidación</h3>", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains(">CONSOLIDACION</td>", nav.Html, StringComparison.Ordinal);
        Assert.Contains(">ACCOUNT</td>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Variante para modelo estándar", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        foreach (var g in new[] { "Planes de cuentas", "Centros de costo y de beneficio", "Monedas y tasas", "Jerarquía de cuentas" })
        {
            Assert.Contains($"<h3>{g}</h3>", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        }

        var monedas = Regex.Match(WebUtility.HtmlDecode(nav.Html), "id=\"capa-Monedas\"(.*?)</details>", RegexOptions.Singleline).Value;
        Assert.Contains("COP y USD", monedas, StringComparison.Ordinal);
        Assert.Contains("Promedio", monedas, StringComparison.Ordinal);
        Assert.Contains("Histórica", monedas, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-21 CA2, HU-22 CA2 y HU-24 CA3: UX-09 muestra cada paso de datos maestros con sus cantidades")]
    [Trait("HU", "HU-21")]
    [Trait("HU", "HU-22")]
    [Trait("HU", "HU-24")]
    public async Task UX09Cantidades()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);

        await GenerarAsync(nav, cliente);

        Assert.Contains("Generación completa.", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"\d+\. Planes de cuentas", nav.Texto);
        Assert.Contains($"PCCO: {Acelerador.Sap.Simulador.SimulatedSapGateway.Cuentas.Count(c => c.Plan == "PCCO")} cuentas cargadas.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("1000: 12 centros de costo y 5 centros de beneficio cargados.", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"PCCO: \d+ nodos cargados\.", nav.Texto);
        Assert.Contains("COP/USD: 27 tasas cargadas", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"\d+\. Modelo BPC, propiedad y scripts", nav.Texto);
    }

    [Fact(DisplayName = "HU-24 CA2: una cuenta de la jerarquía fuera del plan se avisa en el paso sin detener la generación")]
    [Trait("HU", "HU-24")]
    public async Task UX09AvisoJerarquia()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);
        Simulador(app).CuentasHuerfanasJerarquia.Add("PCCO:8888");

        await GenerarAsync(nav, cliente);

        Assert.Contains("PCCO: la jerarquía tiene 8888, que no existe en el plan de cuentas; no se carga.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("<div class=\"note w\">", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Generación completa.", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-23 CA3: una moneda sin tasa deja el paso «Monedas y tasas» con error en UX-09")]
    [Trait("HU", "HU-23")]
    public async Task UX09SinTasa()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);
        Simulador(app).TasasFaltantes.Add("COP:2026-06");

        await GenerarAsync(nav, cliente);

        Assert.Contains("No hay tasa de cierre COP/USD para 2026-06 en el ERP (TCURR).", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Reintentar desde este paso", nav.Texto, StringComparison.Ordinal);
    }
}
