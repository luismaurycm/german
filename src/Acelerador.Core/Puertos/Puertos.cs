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
