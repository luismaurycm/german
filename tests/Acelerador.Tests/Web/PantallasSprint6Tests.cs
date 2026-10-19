using System.Net;
using Acelerador.Core.Puertos;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static Acelerador.Tests.Web.PantallasSprint2Tests;

namespace Acelerador.Tests.Web;

/// <summary>UX-08 y UX-09 con la validación de cuadre, la importación, la conversión y la consolidación (HU-27 a HU-31, HU-36).</summary>
[Collection("Web")]
public class PantallasSprint6Tests
{
    private static readonly Dictionary<string, string?> Nada = [];

    private static FabricaWeb App() => new(PerfilSimulado.ABw75,
        s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(new RelojFijo(new DateTimeOffset(2026, 10, 16, 15, 0, 0, TimeSpan.Zero)))));

    /// <summary>Hasta UX-08 con todas las cuentas mapeadas (el mapeo completo se escribe directo en la base, como lo dejaría UX-06).</summary>
    private static async Task<string> LlegarAPlanCompletoAsync(FabricaWeb app, Navegador nav)
    {
        var cliente = await LlegarAMapeoAsync(nav, Entorno.FilasPrototipo().Take(3));
        var id = Guid.Parse(cliente.Split('/')[^1]);
        var mapeo = app.Services.GetRequiredService<IMapeoRepository>();
        foreach (var plan in new[] { "PCCO", "PCPA" })
        {
            foreach (var g in SimulatedSapGateway.Cuentas.Where(c => c.Plan == plan)
                         .GroupBy(c => c.Descripcion.StartsWith("Utilidad del ejercicio", StringComparison.Ordinal) ? "F-00-302" : Entorno.RubroPorNodo[c.Nodo]))
            {
                await mapeo.GuardarAsync(id, plan, g.Select(c => c.Numero).ToList(), g.Key);
            }
        }

        await nav.PostAsync($"{cliente}/mapeo?handler=Continuar", Nada);
        Assert.Equal($"{cliente}/plan", nav.Ruta);
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

    [Fact(DisplayName = "HU-28 CA3 y HU-31 CA3: UX-08 muestra la variante de los scripts y la cuenta de destino de la diferencia en conversión")]
    [Trait("HU", "HU-28")]
    [Trait("HU", "HU-31")]
    public async Task UX08ScriptsYCuentaCta()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        await LlegarAPlanCompletoAsync(app, nav);
        var html = WebUtility.HtmlDecode(nav.Html);

        Assert.Contains("<h3>Scripts de lógica</h3>", html, StringComparison.Ordinal);
        Assert.Contains(">ELIMINACION_IC.LGF</td>", html, StringComparison.Ordinal);
        Assert.Contains("Variante para modelo estándar (A-BW75-BPC101-STD). RN-02", html, StringComparison.Ordinal);
        Assert.Contains("Diferencia en conversión: cuenta ZACC_CTA", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("no en resultados. RN-18", nav.Texto, StringComparison.Ordinal);
        foreach (var g in new[] { "Validación de cuadre", "Importación a BPC", "Conversión", "Consolidación" })
        {
            Assert.Contains($"<h3>{g}</h3>", html, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "HU-30 CA3, HU-29 CA3 y HU-27 CA4: al terminar, UX-09 dice «Consolidación disponible en moneda local y en dólares»")]
    [Trait("HU", "HU-30")]
    [Trait("HU", "HU-29")]
    [Trait("HU", "HU-27")]
    public async Task UX09Consolidacion()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanCompletoAsync(app, nav);

        await GenerarAsync(nav, cliente);

        Assert.Contains("Consolidación disponible en moneda local y en dólares. Participan 3 sociedades del grupo. RN-06 · RN-07", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"\d+\. Validación de cuadre", nav.Texto);
        Assert.Matches(@"\d+\. Importación a BPC", nav.Texto);
        Assert.Matches(@"\d+\. Conversión a dólares y diferencia en conversión", nav.Texto);
        Assert.Matches(@"\d+\. Consolidación", nav.Texto);
        Assert.Matches(@"1000: \d+ registros importados en 2026-09\.", nav.Texto);
        Assert.Contains("diferencia en conversión en ZACC_CTA", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Eliminaciones intercompañía de G_1000", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Ver reportes", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-36 CA5 y HU-27 CA3: UX-09 lista en «Validación de cuadre» la sociedad rechazada, que no se importa")]
    [Trait("HU", "HU-36")]
    [Trait("HU", "HU-27")]
    public async Task UX09SociedadRechazada()
    {
        using var app = App();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanCompletoAsync(app, nav);
        Simulador(app).AjustesSaldo.Add("2000:11050501:5000000");

        await GenerarAsync(nav, cliente);

        Assert.Contains("2000 no se importa (2026-09): activo − (pasivo + patrimonio) = 5.000.000,00.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("2000 no se importa en 2026-09: no pasó la validación de cuadre (ver C03).", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Participan 2 sociedades del grupo.", nav.Texto, StringComparison.Ordinal);
    }
}
