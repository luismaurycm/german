using System.Net;
using Acelerador.Web;
using Acelerador.Web.Seguridad;
using Acelerador.Tests.Soporte;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Acelerador.Tests.Web;

/// <summary>Las pruebas web van solas: WebApplicationFactory intercepta la construcción del host en el proceso.</summary>
[CollectionDefinition("Web", DisableParallelization = true)]
public class ColeccionWeb;

/// <summary>ADR-02 y ADR-05: solo 127.0.0.1, token por sesión en cookie HttpOnly y SameSite=Strict.</summary>
[Collection("Web")]
public sealed class SeguridadWebTests : IDisposable
{
    private readonly FabricaWeb _app = new();

    public void Dispose() => _app.Dispose();

    private HttpClient SinCookies() => _app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = false });

    [Theory(DisplayName = "ADR-05: sin token de sesión, toda página responde 401")]
    [InlineData("/")]
    [InlineData("/clientes/00000000-0000-0000-0000-000000000001/conexiones")]
    [InlineData("/?t=token-falso")]
    public async Task SinToken(string ruta)
    {
        var r = await SinCookies().GetAsync(ruta);
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Contains("Sesión no válida", await r.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ADR-05: el token válido se cambia por una cookie HttpOnly y SameSite=Strict, y sale de la URL")]
    public async Task TokenACookie()
    {
        var token = _app.Services.GetRequiredService<SesionLocal>().Token;
        var r = await SinCookies().GetAsync($"/?q=caribe&t={token}");

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal("/?q=caribe", r.Headers.Location!.OriginalString);
        var cookie = Assert.Single(r.Headers.GetValues("Set-Cookie"), c => c.StartsWith(SesionLocal.NombreCookie, StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(token, cookie, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ADR-05: con la cookie de sesión la aplicación responde y envía encabezados de endurecimiento")]
    public async Task ConCookie()
    {
        var nav = await _app.NavegadorAsync();
        var r = await nav.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(EncabezadosSeguridad.Csp, r.Headers.GetValues("Content-Security-Policy").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", r.Headers.GetValues("Referrer-Policy").Single());
    }

    [Fact(DisplayName = "ADR-05: cada arranque genera un token distinto")]
    public void TokenAleatorio() => Assert.NotEqual(new SesionLocal().Token, new SesionLocal().Token);

    [Fact(DisplayName = "RNF-Seg-01: un nombre de host distinto de 127.0.0.1 o localhost se rechaza (DNS rebinding)")]
    public async Task HostAjeno()
    {
        var nav = await _app.NavegadorAsync();
        var pedido = new HttpRequestMessage(HttpMethod.Get, "/");
        pedido.Headers.Host = "atacante.example";
        var r = await nav.Http.SendAsync(pedido);
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }

    [Fact(DisplayName = "RNF-Seg-01: un POST sin token antiforgery se rechaza")]
    public async Task SinAntiforgery()
    {
        var nav = await _app.NavegadorAsync();
        var r = await nav.Http.PostAsync("/?handler=Crear", new FormUrlEncodedContent([new("Nombre", "Cliente sin token")]));
        Assert.Equal(HttpStatusCode.BadRequest, r.StatusCode);
    }
}

/// <summary>HU-02 CA6 y ADR-02: la aplicación real escucha solo en 127.0.0.1, sin servidores adicionales.</summary>
[Collection("Web")]
public class ServidorLocalTests
{
    [Fact(DisplayName = "HU-02 CA6 (RNF-Comp-03, ADR-02): Kestrel escucha solo en 127.0.0.1 y la base es un archivo local")]
    [Trait("HU", "HU-02")]
    public async Task SoloLoopback()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "acelerador-pruebas", Guid.NewGuid().ToString("N"));
        var db = Path.Combine(carpeta, "acelerador.db");
        var app = AplicacionAcelerador.Construir([
            "--Acelerador:Puerto=0", $"--Acelerador:BaseDatos={db}", "--Acelerador:AbrirNavegador=false",
            "--Acelerador:AlmacenCredenciales=Memoria", "--Simulador:LatenciaMs=0"]);
        try
        {
            await AplicacionAcelerador.InicializarAsync(app);
            await app.StartAsync();

            var url = Assert.Single(app.Urls);
            Assert.StartsWith("http://127.0.0.1:", url, StringComparison.Ordinal);
            Assert.True(File.Exists(db));

            using var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(url) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync("/")).StatusCode);
            var token = app.Services.GetRequiredService<SesionLocal>().Token;
            Assert.Equal(HttpStatusCode.Redirect, (await http.GetAsync($"/?t={token}")).StatusCode);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
            SqliteConnection.ClearAllPools();
            Directory.Delete(carpeta, recursive: true);
        }
    }
}
