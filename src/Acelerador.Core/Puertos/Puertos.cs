using Acelerador.Core.Dominio;
using Acelerador.Sap;

namespace Acelerador.Core.Puertos;

/// <summary>Clientes en la base local (ADR-04).</summary>
public interface IClienteRepository
{
    Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default);

    Task<Cliente?> ObtenerAsync(Guid id, CancellationToken ct = default);

    Task CrearAsync(Cliente cliente, CancellationToken ct = default);

    Task ActualizarAsync(Cliente cliente, CancellationToken ct = default);
}

/// <summary>Conexiones de cada cliente. Sin credenciales: solo la referencia (RNF-Seg-02).</summary>
public interface IConexionRepository
{
    Task<IReadOnlyList<Conexion>> ListarAsync(Guid clienteId, CancellationToken ct = default);

    Task GuardarAsync(Conexion conexion, CancellationToken ct = default);
}

/// <summary>Registro de ejecuciones (RNF-Aud-01). Base de HU-54.</summary>
public interface IRegistroEjecuciones
{
    Task RegistrarAsync(Ejecucion ejecucion, CancellationToken ct = default);

    Task<IReadOnlyList<Ejecucion>> ListarAsync(Guid? clienteId = null, CancellationToken ct = default);
}

/// <summary>
/// Almacén de credenciales de los usuarios técnicos (HU-04, ADR-04). En Windows es el
/// Administrador de credenciales (cifrado DPAPI por usuario); en pruebas, uno en memoria.
/// </summary>
public interface ICredentialStore
{
    /// <summary>Indica si el almacén conserva las credenciales al cerrar la aplicación.</summary>
    bool EsPersistente { get; }

    void Guardar(string referencia, string usuario, CredencialSap credencial);

    CredencialSap? Leer(string referencia);

    void Eliminar(string referencia);
}

/// <summary>Usuario de Windows que ejecuta el acelerador (ADR-05); queda en el registro.</summary>
public interface IUsuarioActual
{
    string Nombre { get; }
}

/// <summary>
/// Depuración del registro de ejecuciones (RNF-Aud-01). Va aparte de <see cref="IRegistroEjecuciones"/>: los servicios
/// solo agregan filas; esta operación solo quita las que pasaron el plazo de retención.
/// </summary>
public interface IDepuracionRegistro
{
    /// <summary>Quita las entradas con fecha anterior a <paramref name="corte"/> y devuelve cuántas quitó.</summary>
    Task<int> DepurarAnterioresAAsync(DateTimeOffset corte, CancellationToken ct = default);
}

/// <summary>Último inventario de cada cliente (HU-07).</summary>
public interface IInventarioRepository
{
    Task<Inventario?> ObtenerAsync(Guid clienteId, CancellationToken ct = default);

    Task GuardarAsync(Inventario inventario, CancellationToken ct = default);
}

/// <summary>Sociedades del grupo de cada cliente (HU-09, HU-10).</summary>
public interface ISociedadRepository
{
    Task<IReadOnlyList<Sociedad>> ListarAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>Reemplaza todas las sociedades del cliente en una sola transacción.</summary>
    Task GuardarAsync(Guid clienteId, IReadOnlyList<Sociedad> sociedades, CancellationToken ct = default);
}

/// <summary>Mapeo de cuentas a rubros por plan de cuentas (HU-11).</summary>
public interface IMapeoRepository
{
    Task<IReadOnlyList<AsignacionCuenta>> ListarAsync(Guid clienteId, string planCuentas, CancellationToken ct = default);

    /// <summary>Asigna las cuentas al rubro, o las quita de su rubro si <paramref name="rubro"/> es null. Todo o nada.</summary>
    Task GuardarAsync(Guid clienteId, string planCuentas, IReadOnlyList<string> cuentas, string? rubro, CancellationToken ct = default);
}

/// <summary>Ejecuciones del plan de generación (HU-14).</summary>
public interface IEjecucionPlanRepository
{
    Task<EjecucionPlan?> ObtenerUltimaAsync(Guid clienteId, CancellationToken ct = default);

    Task<EjecucionPlan?> ObtenerAsync(Guid id, CancellationToken ct = default);

    /// <summary>Ejecuciones sin terminar (sin fecha de fin), para marcarlas como interrumpidas al arrancar.</summary>
    Task<IReadOnlyList<EjecucionPlan>> ListarAbiertasAsync(CancellationToken ct = default);

    Task GuardarAsync(EjecucionPlan ejecucion, CancellationToken ct = default);
}

/// <summary>Cola que ejecuta el plan en segundo plano para que UX-09 muestre el avance (HU-14).</summary>
public interface IColaEjecuciones
{
    void Encolar(Guid clienteId, Guid ejecucionId);
}

/// <summary>Cargas confirmadas de sociedades no SAP (HU-25). Solo metadatos; los importes van a BW.</summary>
public interface ICargaNoSapRepository
{
    Task<IReadOnlyList<CargaNoSap>> ListarAsync(Guid clienteId, CancellationToken ct = default);

    /// <summary>Reemplaza las cargas de esa sociedad en esos periodos (una carga nueva del periodo reemplaza la anterior).</summary>
    Task GuardarAsync(IReadOnlyList<CargaNoSap> cargas, CancellationToken ct = default);
}
