using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Infra.Credenciales;
using Acelerador.Infra.Datos;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Microsoft.Data.Sqlite;

namespace Acelerador.Tests.Soporte;

/// <summary>Cola de ejecuciones que solo guarda lo encolado; las pruebas la procesan con <see cref="Entorno.ProcesarColaAsync"/>.</summary>
public sealed class ColaManual : IColaEjecuciones
{
    public System.Collections.Concurrent.ConcurrentQueue<(Guid Cliente, Guid Ejecucion)> Pendientes { get; } = new();

    public void Encolar(Guid clienteId, Guid ejecucionId) => Pendientes.Enqueue((clienteId, ejecucionId));
}

public sealed class UsuarioFijo(string nombre = @"CM\consultor1") : IUsuarioActual
{
    public string Nombre { get; } = nombre;
}

/// <summary>Reloj controlable para las pruebas.</summary>
public sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
{
    public DateTimeOffset Ahora { get; set; } = ahora;

    public override DateTimeOffset GetUtcNow() => Ahora;
}

/// <summary>
/// Entorno de prueba de los servicios: SQLite real en una carpeta temporal, almacén de credenciales
/// en memoria y simulador SAP (Regla 6: sin SAP real).
/// </summary>
public sealed class Entorno : IAsyncDisposable
{
    public const string Clave = "Cl4ve-Secreta-Única-987";

    private Entorno(string carpeta, OpcionesSimulador opciones)
    {
        Carpeta = carpeta;
        Opciones = opciones;
        Base = new BaseLocal(Path.Combine(carpeta, "acelerador.db"));
        Clientes = new SqliteClienteRepository(Base);
        Conexiones = new SqliteConexionRepository(Base);
        Registro = new SqliteRegistroEjecuciones(Base);
        Sap = new SimulatedSapGateway(() => Opciones);
        ServicioClientes = new ServicioClientes(Clientes, Registro, Usuario, Reloj);
        ServicioConexiones = new ServicioConexiones(Clientes, Conexiones, Credenciales, Registro, Sap, CatalogoVariantes.VersionUno, Usuario, Reloj);
        ServicioModulos = new ServicioModulos(Clientes);
        Inventarios = new SqliteInventarioRepository(Base);
        Sociedades = new SqliteSociedadRepository(Base);
        Mapeos = new SqliteMapeoRepository(Base);
        Acceso = new AccesoSap(Conexiones, Credenciales);
        ServicioDiagnostico = new ServicioDiagnostico(Clientes, Inventarios, Acceso, Registro, Sap, Usuario, Reloj);
        ServicioSociedades = new ServicioSociedades(Clientes, Sociedades, Acceso, Sap);
        ServicioMapeo = new ServicioMapeo(Clientes, Sociedades, Mapeos, Acceso, Sap);
        ServicioRegistro = new ServicioRegistro(Clientes, Registro, Registro, Reloj);
        Ejecuciones = new SqliteEjecucionPlanRepository(Base);
        ServicioPlan = new ServicioPlan(Clientes, Inventarios, Sociedades, Mapeos, Ejecuciones, GeneradorPlan.Predeterminado);
        ServicioEjecucion = new ServicioEjecucion(Clientes, Ejecuciones, ServicioPlan, Acceso, Sap, Registro, Cola, Usuario, Reloj);
    }

    public SqliteEjecucionPlanRepository Ejecuciones { get; }

    public ColaManual Cola { get; } = new();

    public ServicioPlan ServicioPlan { get; }

    public ServicioEjecucion ServicioEjecucion { get; }

    public SqliteInventarioRepository Inventarios { get; }

    public SqliteSociedadRepository Sociedades { get; }

    public SqliteMapeoRepository Mapeos { get; }

    public AccesoSap Acceso { get; }

    public ServicioDiagnostico ServicioDiagnostico { get; }

    public ServicioSociedades ServicioSociedades { get; }

    public ServicioMapeo ServicioMapeo { get; }

    public ServicioRegistro ServicioRegistro { get; }

    public string Carpeta { get; }

    public OpcionesSimulador Opciones { get; set; }

    public BaseLocal Base { get; }

    public SqliteClienteRepository Clientes { get; }

    public SqliteConexionRepository Conexiones { get; }

    public SqliteRegistroEjecuciones Registro { get; }

    public InMemoryCredentialStore Credenciales { get; } = new();

    public SimulatedSapGateway Sap { get; }

    public UsuarioFijo Usuario { get; } = new();

    public RelojFijo Reloj { get; } = new(new DateTimeOffset(2026, 10, 16, 15, 0, 0, TimeSpan.Zero));

    public ServicioClientes ServicioClientes { get; }

    public ServicioConexiones ServicioConexiones { get; }

    public ServicioModulos ServicioModulos { get; }

    public static async Task<Entorno> CrearAsync(PerfilSimulado perfil = PerfilSimulado.ABw75)
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "acelerador-pruebas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var e = new Entorno(carpeta, new OpcionesSimulador { Perfil = perfil });
        await e.Base.InicializarAsync();
        return e;
    }

    public async Task<Cliente> ClienteAsync(string nombre = "Grupo Ejemplo Andino")
    {
        var r = await ServicioClientes.CrearAsync(nombre);
        return r.Cliente ?? throw new InvalidOperationException(r.Error);
    }

    /// <summary>Datos de conexión del paisaje del prototipo (arquitectura A: BW y BPC en el mismo servidor).</summary>
    public static DatosConexion Datos(SistemaSap sistema, string? host = null, string? credencial = Clave) => sistema switch
    {
        SistemaSap.Erp => new(host ?? "erp-ejemplo.local", "300", "rfc_acel", credencial),
        SistemaSap.Bw => new(host ?? "bw-ejemplo.local", "100", "rfc_acel", credencial),
        _ => new(host ?? "bw-ejemplo.local", "100", "rfc_acel", credencial),
    };

    /// <summary>Registra y prueba las tres conexiones con éxito.</summary>
    public async Task ConectarTodoAsync(Guid clienteId)
    {
        foreach (var s in Enum.GetValues<SistemaSap>())
        {
            await ServicioConexiones.GuardarYProbarAsync(clienteId, s, Datos(s));
        }
    }

    /// <summary>Lleva un cliente hasta Módulos: conexiones, validación ERP–BW, escaneo y Continuar a módulos.</summary>
    public async Task<Cliente> ClienteEnModulosAsync(string nombre = "Grupo Ejemplo Andino")
    {
        var c = await ClienteAsync(nombre);
        await ConectarTodoAsync(c.Id);
        await ServicioConexiones.ValidarErpBwAsync(c.Id);
        await ServicioConexiones.ContinuarADiagnosticoAsync(c.Id);
        await ServicioDiagnostico.EscanearAsync(c.Id);
        var r = await ServicioDiagnostico.GuardarCamposZAsync(c.Id, [], continuar: true);
        Assert.True(r.Exitosa, r.Mensaje);
        return c;
    }

    /// <summary>Filas de UX-05 del prototipo: 1000 matriz, 2000 y 3000 filiales en SAP y 9000 no SAP.</summary>
    public static List<FilaSociedad> FilasPrototipo() =>
    [
        new("1000", "Matriz", true, "COP", "", "PCCO", "100", nameof(MetodoConsolidacion.Matriz)),
        new("2000", "Filial Colombia", true, "COP", "1000", "PCCO", "80", nameof(MetodoConsolidacion.IntegracionGlobal)),
        new("3000", "Filial Panamá", true, "USD", "1000", "PCPA", "100", nameof(MetodoConsolidacion.IntegracionGlobal)),
        new("9000", "Filial no SAP", false, "COP", "1000", "", "35", nameof(MetodoConsolidacion.Participacion)),
    ];

    /// <summary>Lleva un cliente hasta Mapeo de cuentas con las sociedades del prototipo.</summary>
    public async Task<Cliente> ClienteEnMapeoAsync(List<FilaSociedad>? filas = null)
    {
        var c = await ClienteEnModulosAsync();
        await ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        var r = await ServicioSociedades.GuardarAsync(c.Id, filas ?? FilasPrototipo(), continuar: true);
        Assert.True(r.Guardado, string.Join(" ", r.Errores.Select(x => x.Mensaje)) + r.ErrorGuardado);
        return (await Clientes.ObtenerAsync(c.Id))!;
    }

    /// <summary>Lleva un cliente hasta el plan: mapea dos cuentas de cada plan y continúa (sin sociedades no SAP por defecto).</summary>
    public async Task<Cliente> ClienteEnPlanAsync(IReadOnlyList<string>? camposZ = null, List<FilaSociedad>? filas = null)
    {
        var c = await ClienteEnModulosAsync();
        if (camposZ is not null)
        {
            await ServicioDiagnostico.GuardarCamposZAsync(c.Id, camposZ, continuar: false);
        }

        await ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        var r = await ServicioSociedades.GuardarAsync(c.Id, filas ?? FilasPrototipo().Take(3).ToList(), continuar: true);
        Assert.True(r.Guardado, string.Join(" ", r.Errores.Select(x => x.Mensaje)));
        await ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501", "11100501"]);
        await ServicioMapeo.AsignarAsync(c.Id, "3000", "F-00-101", ["1010", "1020"]);
        Assert.NotNull(await ServicioMapeo.ContinuarAsync(c.Id));
        return (await Clientes.ObtenerAsync(c.Id))!;
    }

    /// <summary>Genera y ejecuta lo que haya en la cola (lo que en la aplicación hace la cola en segundo plano).</summary>
    public async Task<EjecucionPlan> GenerarAsync(Guid clienteId)
    {
        var inicio = await ServicioEjecucion.IniciarAsync(clienteId);
        Assert.Null(inicio.Error);
        await ProcesarColaAsync();
        return (await Ejecuciones.ObtenerAsync(inicio.EjecucionId!.Value))!;
    }

    public async Task ProcesarColaAsync()
    {
        while (Cola.Pendientes.TryDequeue(out var item))
        {
            await ServicioEjecucion.EjecutarAsync(item.Cliente, item.Ejecucion);
        }
    }

    public async ValueTask DisposeAsync()
    {
        SqliteConnection.ClearAllPools();
        await Task.Yield();
        try
        {
            Directory.Delete(Carpeta, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}
