using System.Net;
using System.Text.RegularExpressions;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;
using Microsoft.Extensions.DependencyInjection;
using static Acelerador.Tests.Web.PantallasSprint2Tests;

namespace Acelerador.Tests.Web;

/// <summary>UX-08 Plan de generación y UX-09 Ejecución (HU-13, HU-14, HU-15, HU-17) con la cola en segundo plano real.</summary>
[Collection("Web")]
public class PantallasSprint3Tests
{
    private static readonly Dictionary<string, string?> Nada = [];

    /// <summary>UX-01 → UX-06 y Continuar: llega al plan con las sociedades en SAP (sin no SAP, para ir directo a UX-08).</summary>
    private static async Task<string> LlegarAPlanAsync(Navegador nav)
    {
        var cliente = await LlegarAMapeoAsync(nav, Entorno.FilasPrototipo().Take(3));
        await nav.PostAsync($"{cliente}/mapeo",
        [
            new("sociedad", "1000"), new("rubro", "F-00-101"), new("cuentas", "11050501"), new("accion", "asignar"),
        ]);
        await nav.PostAsync($"{cliente}/mapeo?handler=Continuar", Nada);
        Assert.Equal($"{cliente}/plan", nav.Ruta);
        return cliente;
    }

    /// <summary>Espera a que la cola termine la ejecución (máximo 10 s) y deja la página UX-09 cargada.</summary>
    private static async Task EsperarEjecucionAsync(Navegador nav, string cliente)
    {
        for (var i = 0; i < 100; i++)
        {
            await nav.GetAsync($"{cliente}/ejecucion");
            if (!nav.Html.Contains("data-refrescar=\"/", StringComparison.Ordinal))
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail("La ejecución no terminó.");
    }

    private static async Task GenerarAsync(Navegador nav, string cliente)
    {
        await nav.GetAsync($"{cliente}/plan?confirmar=true");
        await nav.PostAsync($"{cliente}/plan?handler=Generar", Nada);
        Assert.Equal($"{cliente}/ejecucion", nav.Ruta);
        await EsperarEjecucionAsync(nav, cliente);
    }

    // ---------- UX-08 Plan de generación (HU-13, HU-17) ----------

    [Fact(DisplayName = "HU-13 CA1: el plan se agrupa en SAP BW, SAP BPC y Monedas y tasas, y marca Nuevo o Existente")]
    [Trait("HU", "HU-13")]
    public async Task UX08Grupos()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await LlegarAPlanAsync(nav);

        Assert.Contains("<h1>Plan de generación</h1>", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        foreach (var capa in new[] { "SAP BW", "SAP BPC", "Monedas y tasas" })
        {
            Assert.Contains($"<h2>{capa}</h2>", nav.Html, StringComparison.Ordinal);
        }

        Assert.Contains("<details class=\"card capa\" open", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Nuevo", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Existente, no se modifica", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("ZACC_C01", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Variante BW 7.5 + BPC 10.1 modelo estándar", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Cierre", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("RN-11", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("RN-03", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-13 CA2: con bodega existente sus objetos se ven «Existente, no se modifica» (RN-03)")]
    [Trait("HU", "HU-13")]
    public async Task UX08Existentes()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        Simulador(app).BodegaExistente = true;
        await LlegarAPlanAsync(nav);

        Assert.Contains("0 objetos a crear", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotMatch(">\\s*Nuevo\\s*<", nav.Html);
    }

    [Fact(DisplayName = "HU-13 CA3: se muestra el total de objetos a crear y Generar pide confirmación con ese número")]
    [Trait("HU", "HU-13")]
    public async Task UX08Confirmacion()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);
        var total = Regex.Match(nav.Texto, @"(\d+) objetos a crear").Groups[1].Value;
        Assert.NotEmpty(total);
        Assert.DoesNotContain("role=\"dialog\"", nav.Html, StringComparison.Ordinal);

        await nav.GetAsync($"{cliente}/plan?confirmar=true");

        Assert.Contains("role=\"dialog\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains($"Se crearán {total} objetos en el sistema del cliente.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("¿Generar?", nav.Texto, StringComparison.Ordinal);
        Assert.Contains(">Cancelar</a>", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-13 CA4 y estado vacío: sin mapeo el plan dice qué falta con un enlace para completarlo")]
    [Trait("HU", "HU-13")]
    public async Task UX08Faltante()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAMapeoAsync(nav, Entorno.FilasPrototipo().Take(3));
        await nav.PostAsync($"{cliente}/mapeo?handler=Continuar", Nada);

        Assert.Contains("Completa los pasos anteriores para ver el plan.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"<a href=\"{cliente}/mapeo\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Mapeo de cuentas: aún no hay cuentas mapeadas a los rubros.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Ir a Mapeo de cuentas", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("objetos a crear", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-13 estado de error: con el escaneo fallido el plan enlaza al diagnóstico")]
    [Trait("HU", "HU-13")]
    public async Task UX08ErrorPasoPrevio()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);
        Simulador(app).FallaInventario = TipoInventario.CamposZ;
        await nav.GetAsync($"{cliente}/diagnostico");
        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Nada);

        await nav.GetAsync($"{cliente}/plan");

        Assert.Contains("Diagnóstico: El último escaneo no terminó.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"href=\"{cliente}/diagnostico\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-13 estado de carga: abrir el plan muestra «Calculando el plan…»")]
    [Trait("HU", "HU-13")]
    public async Task UX08Carga()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);

        Assert.Contains($"href=\"{cliente}/plan\" aria-current=\"step\" data-carga=\"Calculando el plan", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-17 CA1 y CA3: en UX-03 el campo Z incompatible se ve con su tipo y el motivo, y no se puede marcar")]
    [Trait("HU", "HU-17")]
    public async Task UX03CampoIncompatible()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarADiagnosticoAsync(nav);

        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Nada);

        Assert.Matches("id=\"cz-ZZNOTAS\"[^>]*disabled=\"disabled\"", nav.Html);
        Assert.Contains("No se incorpora: Tipo STRG no compatible", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"cz-ZZNOTAS-motivo\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("CHAR 4", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-17 CA2: el campo Z marcado aparece como InfoObject Nuevo en el plan")]
    [Trait("HU", "HU-17")]
    public async Task UX08CampoZ()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarADiagnosticoAsync(nav);
        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Nada);
        await nav.PostAsync($"{cliente}/diagnostico?handler=Campos", new Dictionary<string, string?> { ["campos"] = "ZZREGION", ["accion"] = "continuar" });
        await nav.PostAsync($"{cliente}/modulos", new Dictionary<string, string?> { ["modulos"] = "Consolidacion", ["accion"] = "continuar" });
        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(Entorno.FilasPrototipo().Take(3), "continuar"));
        await nav.PostAsync($"{cliente}/mapeo", [new("sociedad", "1000"), new("rubro", "F-00-101"), new("cuentas", "11050501"), new("accion", "asignar")]);
        await nav.PostAsync($"{cliente}/mapeo?handler=Continuar", Nada);

        Assert.Contains("<h3>Campos Z</h3>", nav.Html, StringComparison.Ordinal);
        Assert.Matches("Nuevo\\s*</span>\\s*</td>\\s*<td data-l=\"Tipo\">InfoObject</td>\\s*<td data-l=\"Nombre t[^\"]*\" class=\"mono\">ZZREGION</td>", nav.Html);
    }

    // ---------- UX-09 Ejecución (HU-14, HU-15) ----------

    [Fact(DisplayName = "HU-14 estado vacío: sin ejecuciones UX-09 dice «Aún no hay ejecuciones»")]
    [Trait("HU", "HU-14")]
    public async Task UX09Vacio()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);
        var c = (await app.Services.GetRequiredService<Acelerador.Core.Puertos.IClienteRepository>().ObtenerAsync(Guid.Parse(cliente.Split('/')[^1])))!;
        c.AvanzarA(Pasos.Ejecucion);
        await app.Services.GetRequiredService<Acelerador.Core.Puertos.IClienteRepository>().ActualizarAsync(c);

        await nav.GetAsync($"{cliente}/ejecucion");

        Assert.Contains("Aún no hay ejecuciones.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Ir al plan de generación", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-14 CA1, CA4 y HU-15 CA4: al terminar cada paso está completo con duración, objetos creados, resumen y Ver reportes")]
    [Trait("HU", "HU-14")]
    public async Task UX09Completa()
    {
        using var app = new FabricaWeb();
        Simulador(app).InventarioVacio = true;
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);

        await GenerarAsync(nav, cliente);

        Assert.Contains("1. Objetos BW y carga full inicial", nav.Texto, StringComparison.Ordinal);
        Assert.Matches(@"\d+ objetos creados", nav.Texto);
        Assert.Matches("<span class=\"mono muted\">\\d+:\\d\\d</span>", nav.Html);
        Assert.Contains("Generación completa.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"href=\"{cliente}/paso/UX-10\">Ver reportes</a>", nav.Html, StringComparison.Ordinal);
        Assert.Matches("<td data-l=\"Objetos\" class=\"mono\">(DTP|Transformaci|InfoCubo|DSO|DataSource)", nav.Html);
        Assert.Contains("Descargar registro", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Completo", nav.Texto, StringComparison.Ordinal);

        await nav.GetAsync("/");
        Assert.Contains("Completo", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-14 CA2 y CA3: un paso con error muestra la causa, Ir a Conexiones y Reintentar; el reintento termina sin repetir")]
    [Trait("HU", "HU-14")]
    public async Task UX09ErrorYReintento()
    {
        using var app = new FabricaWeb();
        Simulador(app).InventarioVacio = true;
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);
        Simulador(app).FallaEscritura.Add(SistemaSap.Bw);

        await GenerarAsync(nav, cliente);

        Assert.Contains("Un paso falló; corrige la causa y reintenta desde ese paso.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("no tiene permiso de escritura en SAP BW", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"href=\"{cliente}/conexiones\">Ir a Conexiones</a>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Reintentar desde este paso", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Con error", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", nav.Html, StringComparison.Ordinal);

        Simulador(app).FallaEscritura.Clear();
        var antes = app.Simulador.Objetos.Count;
        await nav.PostAsync($"{cliente}/ejecucion?handler=Reintentar", Nada);
        await EsperarEjecucionAsync(nav, cliente);

        Assert.Contains("Generación completa.", nav.Texto, StringComparison.Ordinal);
        Assert.Equal(1, antes);
        Assert.Equal(app.Simulador.Objetos.Select(o => (o.Sistema, o.Operacion.TipoObjeto, o.Operacion.NombreTecnico)).Distinct().Count(), app.Simulador.Objetos.Count);
    }

    [Fact(DisplayName = "HU-14 estado de carga: mientras corre, el paso en curso gira, los demás esperan y la página consulta el estado")]
    [Trait("HU", "HU-14")]
    public async Task UX09Carga()
    {
        using var app = new FabricaWeb();
        Simulador(app).InventarioVacio = true;
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);
        Simulador(app).LatenciaEscrituraMs = 300;

        await nav.GetAsync($"{cliente}/plan?confirmar=true");
        await nav.PostAsync($"{cliente}/plan?handler=Generar", Nada);

        Assert.Contains($"data-refrescar=\"{cliente}/ejecucion?handler=Estado\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Ejecutando… paso 1 de 1.", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        var estado = await nav.Http.GetStringAsync($"{cliente}/ejecucion?handler=Estado");
        Assert.Contains("\"estado\":\"EnCurso\"", estado, StringComparison.Ordinal);
        var js = await nav.Http.GetStringAsync("/js/acelerador.js");
        Assert.Contains("data-refrescar", js, StringComparison.Ordinal);
        Simulador(app).LatenciaEscrituraMs = 0;
        await EsperarEjecucionAsync(nav, cliente);
    }

    [Fact(DisplayName = "HU-15 CA2: con bodega existente el paso queda «Existente» sin crear objetos")]
    [Trait("HU", "HU-15")]
    public async Task UX09BodegaExistente()
    {
        using var app = new FabricaWeb();
        Simulador(app).BodegaExistente = true;
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);

        await GenerarAsync(nav, cliente);

        Assert.Contains("0 objetos creados", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Existente", nav.Texto, StringComparison.Ordinal);
        Assert.Empty(app.Simulador.Objetos);
    }

    [Fact(DisplayName = "WCAG 2.1 AA básico en UX-08 y UX-09: secciones con teclado, controles con nombre y tablas con título")]
    public async Task Accesibilidad()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav);
        await GenerarAsync(nav, cliente);

        foreach (var ruta in new[] { "plan", "plan?confirmar=true", "ejecucion" })
        {
            await nav.GetAsync($"{cliente}/{ruta}");
            Assert.Single(Regex.Matches(nav.Html, "<h1>"));
            Assert.DoesNotContain(" style=\"", nav.Html, StringComparison.Ordinal);
            Assert.Equal(Regex.Count(nav.Html, "<table"), Regex.Count(nav.Html, "<caption"));
        }

        await nav.GetAsync($"{cliente}/plan");
        Assert.Equal(3, Regex.Count(nav.Html, "<summary>"));
    }
}
