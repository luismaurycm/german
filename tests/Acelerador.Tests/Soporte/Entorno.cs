using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Infra.Credenciales;
using Acelerador.Infra.Datos;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Microsoft.Data.Sqlite;

namespace Acelerador.Tests.Soporte;

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
    }

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
