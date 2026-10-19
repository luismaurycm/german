using Acelerador.Core.Generacion;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Infra;
using Acelerador.Infra.Credenciales;
using Acelerador.Infra.Datos;
using Acelerador.Sap;
using Acelerador.Sap.Nco;
using Acelerador.Sap.Simulador;
using Acelerador.Web.Seguridad;
using Microsoft.Extensions.Options;

namespace Acelerador.Web;

/// <summary>Opciones de la sección «Acelerador» de appsettings.</summary>
public sealed class OpcionesAcelerador
{
    public const string Seccion = "Acelerador";

    /// <summary>Puerto en 127.0.0.1. 0 = cualquiera libre.</summary>
    public int Puerto { get; set; } = 5080;

    /// <summary>Ruta del archivo SQLite. Vacío = perfil del usuario (ADR-04).</summary>
    public string? BaseDatos { get; set; }

    /// <summary>Auto (Windows en Windows, memoria en otros), Windows o Memoria.</summary>
    public string AlmacenCredenciales { get; set; } = "Auto";

    public bool AbrirNavegador { get; set; } = true;
}

/// <summary>Une los módulos del monolito (ADR-01) en el contenedor de dependencias.</summary>
public static class Composicion
{
    public static IServiceCollection AddAcelerador(this IServiceCollection services, IConfiguration config)
    {
        var opciones = config.GetSection(OpcionesAcelerador.Seccion).Get<OpcionesAcelerador>() ?? new OpcionesAcelerador();
        services.Configure<OpcionesAcelerador>(config.GetSection(OpcionesAcelerador.Seccion));
        services.Configure<OpcionesSimulador>(config.GetSection(OpcionesSimulador.Seccion));

        services.AddSingleton<SesionLocal>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IUsuarioActual, UsuarioSistemaOperativo>();
        services.AddSingleton(CatalogoVariantes.VersionUno);

        services.AddSingleton(new BaseLocal(string.IsNullOrWhiteSpace(opciones.BaseDatos) ? BaseLocal.RutaPorDefecto() : opciones.BaseDatos));
        services.AddSingleton<IClienteRepository, SqliteClienteRepository>();
        services.AddSingleton<IConexionRepository, SqliteConexionRepository>();
        services.AddSingleton<SqliteRegistroEjecuciones>();
        services.AddSingleton<IRegistroEjecuciones>(sp => sp.GetRequiredService<SqliteRegistroEjecuciones>());
        services.AddSingleton<IDepuracionRegistro>(sp => sp.GetRequiredService<SqliteRegistroEjecuciones>());
        services.AddSingleton<IInventarioRepository, SqliteInventarioRepository>();
        services.AddSingleton<ISociedadRepository, SqliteSociedadRepository>();
        services.AddSingleton<IMapeoRepository, SqliteMapeoRepository>();
        services.AddSingleton<IEjecucionPlanRepository, SqliteEjecucionPlanRepository>();
        services.AddSingleton<ICargaNoSapRepository, SqliteCargaNoSapRepository>();
        services.AddSingleton<CargasPendientes>();
        services.AddSingleton(GeneradorPlan.Predeterminado);
        services.AddSingleton<ColaEjecuciones>();
        services.AddSingleton<IColaEjecuciones>(sp => sp.GetRequiredService<ColaEjecuciones>());
        services.AddHostedService(sp => sp.GetRequiredService<ColaEjecuciones>());
        services.AddSingleton(_ => CrearAlmacen(opciones.AlmacenCredenciales));

        var modoSap = config["Sap:Modo"] ?? "Simulado";
        services.AddSingleton<ISapGateway>(sp => string.Equals(modoSap, "Nco", StringComparison.OrdinalIgnoreCase)
            ? new NcoSapGateway()
            : new SimulatedSapGateway(() => sp.GetRequiredService<IOptionsMonitor<OpcionesSimulador>>().CurrentValue));

        services.AddScoped<ServicioClientes>();
        services.AddScoped<ServicioConexiones>();
        services.AddScoped<ServicioModulos>();
        services.AddScoped<AccesoSap>();
        services.AddScoped<ServicioDiagnostico>();
        services.AddScoped<ServicioSociedades>();
        services.AddScoped<ServicioMapeo>();
        services.AddScoped<ServicioRegistro>();
        services.AddScoped<ServicioPlan>();
        services.AddScoped<ServicioEjecucion>();
        services.AddScoped<ServicioNoSap>();
        return services;
    }

    private static ICredentialStore CrearAlmacen(string modo)
    {
        if (string.Equals(modo, "Memoria", StringComparison.OrdinalIgnoreCase))
        {
            return new InMemoryCredentialStore();
        }

        if (OperatingSystem.IsWindows())
        {
            return new WindowsCredentialStore();
        }

        if (string.Equals(modo, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            throw new PlatformNotSupportedException("El Administrador de credenciales de Windows solo existe en Windows.");
        }

        return new InMemoryCredentialStore();
    }
}
