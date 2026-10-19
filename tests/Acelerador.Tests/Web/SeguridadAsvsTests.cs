using System.Net;
using System.Text;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Tests.Core;
using Acelerador.Tests.Soporte;
using Microsoft.Data.Sqlite;

namespace Acelerador.Tests.Web;

/// <summary>HU-55: controles de OWASP ASVS nivel 2 que se verifican en cada build (RNF-Seg-01).</summary>
[Collection("Web")]
public class SeguridadAsvsTests
{
    [Fact(DisplayName = "HU-55 CA1: una petición de más de 4 MB se rechaza con 413; la subida no SAP admite hasta 20 MB")]
    [Trait("HU", "HU-55")]
    public async Task LimiteDePeticion()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var grande = new ByteArrayContent(new byte[(4 * 1024 * 1024) + 1]);
        grande.Headers.ContentType = new("application/x-www-form-urlencoded");

        var r = await nav.Http.PostAsync("/?handler=Crear", grande);
        var archivo = new MultipartFormDataContent { { new StringContent("9000"), "sociedad" }, { new ByteArrayContent(new byte[5 * 1024 * 1024]), "archivo", "9000.csv" } };
        var subida = await nav.Http.PostAsync("/clientes/00000000-0000-0000-0000-000000000000/no-sap?handler=Subir", archivo);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
        Assert.NotEqual(HttpStatusCode.RequestEntityTooLarge, subida.StatusCode);
    }

    [Theory(DisplayName = "HU-55 CA2: el archivo no SAP se valida en el servidor: solo CSV de texto")]
    [Trait("HU", "HU-55")]
    [InlineData("9000.xlsx", false)]
    [InlineData("9000.exe", false)]
    [InlineData("9000.csv", true)]
    public void SoloCsv(string nombre, bool binario)
    {
        var contenido = binario ? [0x50, 0x4B, 0x03, 0x04, 0x00, 0x00] : Encoding.UTF8.GetBytes("sociedad;cuenta;periodo;tercero;ceco;cebe;importe\n9000;11050501;2026-09;;;;1\n");

        var r = LectorArchivoNoSap.Leer(contenido, nombre, "9000", ["9000"], new HashSet<string> { "11050501" });

        Assert.Equal(LectorArchivoNoSap.MensajeNoEsCsv, Assert.Single(r.Errores).Motivo);
        Assert.Empty(r.Filas);
    }

    [Fact(DisplayName = "HU-55 CA2: un formulario manipulado (sociedad que no es del grupo) se rechaza en el servidor")]
    [Trait("HU", "HU-55")]
    public async Task FormularioManipulado()
    {
        var (e2, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e2;

        var r = await e2.ServicioCierre.CambiarBloqueoAsync(c.Id, "2026-09", "9999", bloquear: true);
        var asiento = await e2.ServicioAsientos.GuardarAsync(c.Id, "2026-09", "COP", null, [new("9999", "PCCO_11050501", null, "1", null, ""), new("1000", "PCCO_11050501", null, null, "1", "")]);

        Assert.Equal((false, "9999 no es una sociedad del grupo."), (r.Exitosa, r.Mensaje));
        Assert.Null(asiento.Numero);
        Assert.DoesNotContain(e2.Sap.Almacen.Bloqueos, b => b.Sociedad == "9999");
    }

    [Fact(DisplayName = "HU-55 CA3: el CI falla si alguna dependencia tiene vulnerabilidades conocidas")]
    [Trait("HU", "HU-55")]
    public void DependenciasSinVulnerabilidades()
    {
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !Directory.Exists(Path.Combine(raiz.FullName, ".github")))
        {
            raiz = raiz.Parent;
        }

        var ci = File.ReadAllText(Path.Combine(raiz!.FullName, ".github", "workflows", "ci.yml"));
        Assert.Contains("dotnet list", ci, StringComparison.Ordinal);
        Assert.Contains("--vulnerable --include-transitive", ci, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-55 CA4 (Ley 1581/2012): los terceros de una carga no SAP no quedan en la base local ni en el registro")]
    [Trait("HU", "HU-55")]
    public async Task TercerosNoQuedanEnLaBaseLocal()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: true);
        await e.MapearTodoAsync(c.Id);
        await e.ServicioMapeo.ContinuarAsync(c.Id);
        const string tercero = "NIT-800123456";
        await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "9000.csv", LectorArchivoNoSapTests.Csv($"9000;11050501;2026-09;{tercero};;;1000", $"9000;31050501;2026-09;{tercero};;;-1000"));

        Assert.True((await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000")).Exitosa);

        SqliteConnection.ClearAllPools();
        foreach (var archivo in Directory.GetFiles(e.Carpeta, "*", SearchOption.AllDirectories))
        {
            using var s = new FileStream(archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var m = new MemoryStream();
            s.CopyTo(m);
            Assert.DoesNotContain(tercero, Encoding.UTF8.GetString(m.ToArray()), StringComparison.Ordinal);
        }

        Assert.DoesNotContain(await e.Registro.ListarAsync(c.Id), x => x.Mensaje.Contains(tercero, StringComparison.Ordinal) || x.Objetos.Contains(tercero, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "HU-55 CA2: subir un archivo que no es CSV muestra el error y no deja vista previa")]
    [Trait("HU", "HU-55")]
    public async Task SubirNoCsv()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await PantallasSprint4Tests.LlegarANoSapAsync(nav);

        await nav.SubirAsync($"{cliente}/no-sap?handler=Subir", new Dictionary<string, string> { ["sociedad"] = "9000" }, "archivo", "9000.xlsx", [0x50, 0x4B, 0x03, 0x04, 0x00]);

        Assert.Contains(LectorArchivoNoSap.MensajeNoEsCsv, WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
    }
}
