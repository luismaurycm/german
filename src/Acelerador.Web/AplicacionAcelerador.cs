using System.Diagnostics;
using System.Net;
using Acelerador.Core.Servicios;
using Acelerador.Infra.Datos;
using Acelerador.Web.Seguridad;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Acelerador.Web;

/// <summary>Arma la aplicación web local (ADR-02): Kestrel solo en 127.0.0.1, token de sesión y Razor Pages.</summary>
public static class AplicacionAcelerador
{
    public static WebApplication Construir(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Solo loopback (ADR-02, ADR-05): la aplicación no es alcanzable desde la red.
        var puerto = builder.Configuration.GetValue($"{OpcionesAcelerador.Seccion}:Puerto", 5080);
        builder.WebHost.ConfigureKestrel(k =>
        {
            k.Listen(IPAddress.Loopback, puerto);
            k.AddServerHeader = false;
        });

        builder.Services.AddAcelerador(builder.Configuration);
        builder.Services.AddRazorPages(o => o.Conventions.ConfigureFilter(new AutoValidateAntiforgeryTokenAttribute()));
        builder.Services.AddAntiforgery(o =>
        {
            o.Cookie.Name = "acelerador_af";
            o.Cookie.SameSite = SameSiteMode.Strict;
            o.Cookie.HttpOnly = true;
        });

        var app = builder.Build();

        app.UseExceptionHandler("/error");
        app.UseStatusCodePagesWithReExecute("/error", "?codigo={0}");
        app.UseEncabezadosSeguridad();
        app.UseStaticFiles();
        app.UseMiddleware<MiddlewareSesion>();
        app.UseRouting();
        app.MapRazorPages();

        app.Lifetime.ApplicationStarted.Register(() => Anunciar(app));
        return app;
    }

    /// <summary>
    /// Crea o migra la base local, depura el registro de más de 12 meses (RNF-Aud-01) y marca como interrumpidas las
    /// ejecuciones que quedaron a medias.
    /// </summary>
    public static async Task InicializarAsync(WebApplication app)
    {
        await app.Services.GetRequiredService<BaseLocal>().InicializarAsync();
        using var scope = app.Services.CreateScope();
        var depuradas = await scope.ServiceProvider.GetRequiredService<ServicioRegistro>().DepurarAsync();
        if (depuradas > 0)
        {
            app.Logger.LogInformation("Registro de ejecuciones: {Cantidad} entradas de más de 12 meses depuradas", depuradas);
        }

        var interrumpidas = await scope.ServiceProvider.GetRequiredService<ServicioEjecucion>().MarcarInterrumpidasAsync();
        if (interrumpidas > 0)
        {
            app.Logger.LogWarning("{Cantidad} ejecuciones quedaron interrumpidas al cerrar el acelerador; se pueden reintentar", interrumpidas);
        }
    }

    private static void Anunciar(WebApplication app)
    {
        var direccion = app.Services.GetService<IServer>()?.Features.Get<IServerAddressesFeature>()?.Addresses.FirstOrDefault();
        if (direccion is null || !direccion.StartsWith("http://127.0.0.1:", StringComparison.Ordinal))
        {
            return;
        }

        // El enlace lleva el token de sesión: se escribe en la consola, no en el registro de la aplicación.
        var url = app.Services.GetRequiredService<SesionLocal>().UrlDeInicio(direccion);
        Console.WriteLine();
        Console.WriteLine("Acelerador de consolidación listo. Abre este enlace en el navegador (solo funciona en este equipo):");
        Console.WriteLine($"  {url}");
        Console.WriteLine();

        if (app.Services.GetRequiredService<IOptions<OpcionesAcelerador>>().Value.AbrirNavegador)
        {
            AbrirNavegador(url);
        }
    }

    private static void AbrirNavegador(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", url);
            }
            else
            {
                Process.Start("xdg-open", url);
            }
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Console.WriteLine("No se pudo abrir el navegador; copia el enlace de arriba.");
        }
    }
}
