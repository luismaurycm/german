using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Core;
using Acelerador.Tests.Soporte;
using static Acelerador.Tests.Web.PantallasSprint2Tests;

namespace Acelerador.Tests.Web;

/// <summary>UX-07 (HU-25) y el plan y la ejecución con BW/4HANA, activos fijos y deterioros (HU-16, HU-19, HU-20).</summary>
[Collection("Web")]
public class PantallasSprint4Tests
{
    private static readonly Dictionary<string, string?> Nada = [];

    /// <summary>Hasta UX-07: sociedades del prototipo (con 9000 no SAP) y cuentas mapeadas para el archivo.</summary>
    private static async Task<string> LlegarANoSapAsync(Navegador nav)
    {
        var cliente = await LlegarAMapeoAsync(nav);
        await nav.PostAsync($"{cliente}/mapeo", [new("sociedad", "1000"), new("rubro", "F-00-101"), new("cuentas", "11050501"), new("cuentas", "11100501"), new("accion", "asignar")]);
        await nav.PostAsync($"{cliente}/mapeo?handler=Continuar", Nada);
        Assert.Equal($"{cliente}/no-sap", nav.Ruta);
        return cliente;
    }

    private static Task SubirAsync(Navegador nav, string cliente, byte[] contenido, string nombre = "9000_2026_09.csv") =>
        nav.SubirAsync($"{cliente}/no-sap?handler=Subir", new Dictionary<string, string> { ["sociedad"] = "9000" }, "archivo", nombre, contenido);

    private static async Task<string> LlegarAPlanAsync(Navegador nav, params string[] modulos)
    {
        var cliente = await LlegarAModulosAsync(nav);
        await nav.PostAsync($"{cliente}/modulos", modulos.Select(m => new KeyValuePair<string, string>("modulos", m)).Append(new("accion", "continuar")));
        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(Entorno.FilasPrototipo().Take(3), "continuar"));
        await nav.PostAsync($"{cliente}/mapeo", [new("sociedad", "1000"), new("rubro", "F-00-101"), new("cuentas", "11050501"), new("accion", "asignar")]);
        await nav.PostAsync($"{cliente}/mapeo?handler=Continuar", Nada);
        Assert.Equal($"{cliente}/plan", nav.Ruta);
        return cliente;
    }

    // ---------- UX-07 (HU-25) ----------

    [Fact(DisplayName = "HU-25 estado vacío: sin sociedades no SAP UX-07 lo dice y el paso no está en la navegación")]
    [Trait("HU", "HU-25")]
    public async Task UX07Vacio()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAMapeoAsync(nav, Entorno.FilasPrototipo().Take(3));
        await nav.PostAsync($"{cliente}/mapeo", [new("sociedad", "1000"), new("rubro", "F-00-101"), new("cuentas", "11050501"), new("accion", "asignar")]);
        await nav.PostAsync($"{cliente}/mapeo?handler=Continuar", Nada);

        await nav.GetAsync($"{cliente}/no-sap");

        Assert.Contains("Ninguna sociedad está marcada como no SAP.", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\d\. Sociedades no SAP", nav.Texto);
    }

    [Fact(DisplayName = "HU-25 CA1: se sube el archivo de la sociedad no SAP y se ve la vista previa con sus columnas")]
    [Trait("HU", "HU-25")]
    public async Task UX07VistaPrevia()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarANoSapAsync(nav);
        Assert.Contains("<input id=\"archivo-9000\" type=\"file\" name=\"archivo\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Pendiente", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("por definir", nav.Texto, StringComparison.Ordinal);

        await SubirAsync(nav, cliente, LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;800123456;CC100;PC10;12500000", "9000;11100501;2026-09;;CC100;PC20;348900000"));

        foreach (var col in new[] { "Sociedad", "Cuenta", "Periodo", "Tercero", "CeCo", "CeBe", "Importe" })
        {
            Assert.Matches($"<th scope=\"col\"( class=\"num\")?>{col}</th>", nav.Html);
        }

        Assert.Contains("Validado · 0 errores", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("800123456", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("348.900.000", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("2 filas sin errores", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Confirmar carga</button>", Regex.Match(nav.Html, "<button[^>]*disabled[^>]*>Confirmar carga</button>").Value, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-25 CA2 y CA3: los errores salen por fila, columna y motivo, con «cuenta sin mapear», y Confirmar queda deshabilitado")]
    [Trait("HU", "HU-25")]
    public async Task UX07Errores()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarANoSapAsync(nav);

        await SubirAsync(nav, cliente, LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;;;1", "9000;11050501;2026-09;;;;abc", "9000;11050501;2026-09;;;;1", "9000;9999;2026-09;;;;1"));

        Assert.Contains("Con errores", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("2 errores en el archivo.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("<th scope=\"col\">Fila</th><th scope=\"col\">Columna</th><th scope=\"col\">Motivo</th>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("«abc» no es un importe numérico.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("cuenta sin mapear", nav.Texto, StringComparison.Ordinal);
        Assert.Matches("<button class=\"btn btn-p\" type=\"submit\" disabled=\"disabled\"[^>]*>Confirmar carga</button>", nav.Html);
        Assert.Contains("role=\"alert\"", nav.Html, StringComparison.Ordinal);

        // Aunque alguien envíe Confirmar, el servidor no carga.
        await nav.PostAsync($"{cliente}/no-sap?handler=Confirmar", new Dictionary<string, string?> { ["sociedad"] = "9000" });
        Assert.Contains("Corrige los errores del archivo antes de confirmar la carga.", nav.Texto, StringComparison.Ordinal);
        Assert.Empty(app.Simulador.Datos);
    }

    [Fact(DisplayName = "HU-25 CA3 y CA1: con el archivo válido se confirma la carga, la sociedad queda cargada y se continúa al plan")]
    [Trait("HU", "HU-25")]
    public async Task UX07Confirmar()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarANoSapAsync(nav);
        Assert.Matches("<button class=\"btn btn-p\" type=\"submit\" disabled=\"disabled\"[^>]*>Continuar al plan</button>", nav.Html);

        await SubirAsync(nav, cliente, LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;;;100"));
        await nav.PostAsync($"{cliente}/no-sap?handler=Confirmar", new Dictionary<string, string?> { ["sociedad"] = "9000" });

        Assert.Contains("Carga de 9000 confirmada: 1 fila.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Cargada · 1 fila", nav.Texto, StringComparison.Ordinal);
        Assert.Single(app.Simulador.Datos);
        await nav.PostAsync($"{cliente}/no-sap?handler=Continuar", Nada);
        Assert.Equal($"{cliente}/plan", nav.Ruta);
        Assert.Contains("Sociedades no SAP", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-25 CA4: un archivo con filas de otra sociedad no registrada se rechaza (RN-07)")]
    [Trait("HU", "HU-25")]
    public async Task UX07OtraSociedad()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarANoSapAsync(nav);

        await SubirAsync(nav, cliente, LectorArchivoNoSapTests.Csv("D999;11050501;2026-09;;;;1"));

        Assert.Contains("D999 no es una sociedad no SAP registrada en Sociedades. Solo se cargan sociedades del grupo (RN-07).", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-25 estado de error: un archivo que no es UTF-8 se rechaza con el motivo")]
    [Trait("HU", "HU-25")]
    public async Task UX07Codificacion()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarANoSapAsync(nav);

        await SubirAsync(nav, cliente, Encoding.Latin1.GetBytes("sociedad;cuenta;periodo;tercero;ceco;cebe;importe\r\n9000;11050501;2026-09;Compañía;;;1\r\n"));

        Assert.Contains("El archivo no está en UTF-8.", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-25 estado de carga: la subida muestra una barra de progreso y la zona acepta arrastrar y soltar")]
    [Trait("HU", "HU-25")]
    public async Task UX07Carga()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarANoSapAsync(nav);

        Assert.Contains("data-subida", nav.Html, StringComparison.Ordinal);
        Assert.Contains("<progress id=\"prog-9000\" max=\"100\" value=\"0\">", nav.Html, StringComparison.Ordinal);
        Assert.Contains("<label for=\"prog-9000\">Subiendo archivo de 9000</label>", nav.Html, StringComparison.Ordinal);
        var js = await nav.Http.GetStringAsync("/js/acelerador.js");
        Assert.Contains("xhr.upload.addEventListener('progress'", js, StringComparison.Ordinal);
        Assert.Contains("'drop'", js, StringComparison.Ordinal);
        var plantilla = await nav.Http.GetStringAsync($"{cliente}/no-sap?handler=Plantilla&sociedad=9000");
        Assert.Contains("sociedad;cuenta;periodo;tercero;ceco;cebe;importe", plantilla, StringComparison.Ordinal);
    }

    // ---------- UX-08 y UX-09 (HU-16, HU-19, HU-20) ----------

    [Fact(DisplayName = "HU-16 CA4: en BW/4HANA el plan muestra «aDSO» en los InfoProviders")]
    [Trait("HU", "HU-16")]
    public async Task UX08Adso()
    {
        using var app = new FabricaWeb(PerfilSimulado.ABw4Hana);
        var nav = await app.NavegadorAsync();
        await LlegarAPlanAsync(nav, "Consolidacion");

        Assert.Contains("<td data-l=\"Tipo\">aDSO</td>", nav.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<td data-l=\"Tipo\">InfoCubo</td>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Conectado por ODP", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Variante BW/4HANA + BPC 2021 modelo estándar", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-19 CA3 y HU-20 CA3: el plan y la ejecución muestran «Activos fijos en BW» y «Deterioros en BW»")]
    [Trait("HU", "HU-19")]
    [Trait("HU", "HU-20")]
    public async Task UX08yUX09Modulos()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAPlanAsync(nav, "Consolidacion", "ActivosFijos", "Deterioros");

        Assert.Contains("<h3>Activos fijos</h3>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("<h3>Deterioros</h3>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("<h3>Cadenas de proceso</h3>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("ZACC_PC_DELTA", nav.Texto, StringComparison.Ordinal);

        await nav.GetAsync($"{cliente}/plan?confirmar=true");
        await nav.PostAsync($"{cliente}/plan?handler=Generar", Nada);
        for (var i = 0; i < 100 && nav.Html.Contains("data-refrescar=\"/", StringComparison.Ordinal); i++)
        {
            await Task.Delay(100);
            await nav.GetAsync($"{cliente}/ejecucion");
        }

        Assert.Matches(@"\d+\. Activos fijos en BW", nav.Texto);
        Assert.Matches(@"\d+\. Deterioros en BW", nav.Texto);
        Assert.Contains("Generación completa.", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "WCAG 2.1 AA básico en UX-07: controles con nombre, tablas con título y sin estilos en línea")]
    public async Task Accesibilidad()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarANoSapAsync(nav);
        await SubirAsync(nav, cliente, LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;;;1", "9000;9999;2026-09;;;;1"));

        Assert.Single(Regex.Matches(nav.Html, "<h1>"));
        Assert.DoesNotContain(" style=\"", nav.Html, StringComparison.Ordinal);
        Assert.Equal(Regex.Count(nav.Html, "<table"), Regex.Count(nav.Html, "<caption"));
        foreach (Match m in Regex.Matches(nav.Html, "<(input|select|progress)\\b[^>]*>"))
        {
            if (m.Value.Contains("type=\"hidden\"", StringComparison.Ordinal))
            {
                continue;
            }

            var id = Regex.Match(m.Value, "id=\"([^\"]+)\"").Groups[1].Value;
            Assert.True(m.Value.Contains("aria-label=", StringComparison.Ordinal) || nav.Html.Contains($"for=\"{id}\"", StringComparison.Ordinal), m.Value);
        }
    }
}
