using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Acelerador.Core.Puertos;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Web.Seguridad;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Acelerador.Tests.Soporte;

/// <summary>Captura todo lo que la aplicación escribe en su registro, para comprobar que no lleva credenciales.</summary>
public sealed class RegistroCapturado : ILoggerProvider
{
    public ConcurrentQueue<string> Lineas { get; } = new();

    public ILogger CreateLogger(string categoryName) => new Capturador(this, categoryName);

    public void Dispose()
    {
    }

    private sealed class Capturador(RegistroCapturado dueño, string categoria) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            dueño.Lineas.Enqueue($"{categoria} {logLevel}: {formatter(state, exception)} {exception}");
    }
}

/// <summary>Aplicación web completa en memoria, con SQLite temporal, simulador SAP y almacén en memoria.</summary>
public sealed class FabricaWeb : WebApplicationFactory<Program>
{
    private readonly PerfilSimulado _perfil;
    private readonly Action<IServiceCollection>? _servicios;

    public FabricaWeb(PerfilSimulado perfil = PerfilSimulado.ABw75, Action<IServiceCollection>? servicios = null)
    {
        _perfil = perfil;
        _servicios = servicios;
        Carpeta = Path.Combine(Path.GetTempPath(), "acelerador-pruebas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Carpeta);
    }

    public string Carpeta { get; }

    public string RutaBase => Path.Combine(Carpeta, "acelerador.db");

    public RegistroCapturado Registro { get; } = new();

    public SimulatedSapGateway Simulador => (SimulatedSapGateway)Services.GetRequiredService<ISapGateway>();

    public ICredentialStore Credenciales => Services.GetRequiredService<ICredentialStore>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Acelerador:BaseDatos", RutaBase);
        builder.UseSetting("Acelerador:AbrirNavegador", "false");
        builder.UseSetting("Acelerador:AlmacenCredenciales", "Memoria");
        builder.UseSetting("Simulador:Perfil", _perfil.ToString());
        builder.UseSetting("Simulador:LatenciaMs", "0");

        // Todo el registro, también el de ASP.NET Core, para buscar credenciales en él (HU-04 CA2).
        builder.UseSetting("Logging:LogLevel:Default", "Trace");
        builder.UseSetting("Logging:LogLevel:Microsoft.AspNetCore", "Trace");
        builder.ConfigureLogging(l => l.AddProvider(Registro).SetMinimumLevel(LogLevel.Trace));
        if (_servicios is not null)
        {
            builder.ConfigureServices(_servicios);
        }
    }

    /// <summary>Cliente HTTP ya autenticado con el token de sesión, sin seguir redirecciones.</summary>
    public async Task<Navegador> NavegadorAsync()
    {
        var http = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });
        var token = Services.GetRequiredService<SesionLocal>().Token;
        var r = await http.GetAsync($"/?{SesionLocal.ParametroToken}={token}");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        return new Navegador(http);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try
        {
            Directory.Delete(Carpeta, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

/// <summary>Envoltura mínima para recorrer las pantallas: GET, POST con antiforgery y seguir redirecciones.</summary>
public sealed partial class Navegador(HttpClient http)
{
    public HttpClient Http { get; } = http;

    public string Html { get; private set; } = "";

    public string Ruta { get; private set; } = "/";

    public async Task<HttpResponseMessage> GetAsync(string ruta)
    {
        var r = await Http.GetAsync(ruta);
        Ruta = ruta;
        Html = await r.Content.ReadAsStringAsync();
        return r;
    }

    /// <summary>POST a un formulario de la página actual. Sigue la redirección (PRG) si la hay.</summary>
    public async Task<HttpResponseMessage> PostAsync(string accion, IDictionary<string, string?> campos, bool seguir = true)
    {
        var token = TokenAntiforgery().Match(Html);
        Assert.True(token.Success, "La página no tiene token antiforgery.");
        var datos = campos.Where(c => c.Value is not null).Select(c => new KeyValuePair<string, string>(c.Key, c.Value!)).ToList();
        datos.Add(new("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)));

        var r = await Http.PostAsync(accion, new FormUrlEncodedContent(datos));
        if (seguir && r.StatusCode == HttpStatusCode.Redirect)
        {
            var destino = r.Headers.Location!.OriginalString.Split('#')[0];
            return await GetAsync(destino);
        }

        Html = await r.Content.ReadAsStringAsync();
        return r;
    }

    /// <summary>Texto visible aproximado (sin etiquetas ni espacios repetidos), con entidades decodificadas.</summary>
    public string Texto => Espacios().Replace(WebUtility.HtmlDecode(Etiquetas().Replace(Html, " ")), " ");

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenAntiforgery();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Etiquetas();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}
