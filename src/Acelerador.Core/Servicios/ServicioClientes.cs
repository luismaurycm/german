using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;

namespace Acelerador.Core.Servicios;

/// <summary>Fila de UX-01: el cliente y su última ejecución registrada.</summary>
public sealed record ClienteResumen(Cliente Cliente, Ejecucion? UltimaEjecucion)
{
    public DateTimeOffset? Fecha => UltimaEjecucion?.Fecha;

    public string Usuario => UltimaEjecucion?.Usuario ?? Cliente.CreadoPor;
}

public sealed record ListaClientes(int Total, IReadOnlyList<ClienteResumen> Filas);

public sealed record ResultadoCreacion(Cliente? Cliente, string? Error);

/// <summary>Casos de uso de HU-02 (UX-01).</summary>
public sealed class ServicioClientes(
    IClienteRepository clientes,
    IRegistroEjecuciones registro,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public async Task<ListaClientes> ListarAsync(string? busqueda, CancellationToken ct = default)
    {
        var todos = await clientes.ListarAsync(ct);
        var ejecuciones = await registro.ListarAsync(null, ct);
        var ultimas = ejecuciones
            .GroupBy(e => e.ClienteId)
            .ToDictionary(g => g.Key, g => g.MaxBy(e => e.Fecha));

        var filas = ReglasCliente.Filtrar(todos, busqueda)
            .Select(c => new ClienteResumen(c, ultimas.GetValueOrDefault(c.Id)))
            .ToList();
        return new ListaClientes(todos.Count, filas);
    }

    public async Task<ResultadoCreacion> CrearAsync(string? nombre, CancellationToken ct = default)
    {
        var existentes = await clientes.ListarAsync(ct);
        var error = ReglasCliente.ValidarNombre(nombre, existentes.Select(c => c.Nombre));
        if (error is not null)
        {
            return new ResultadoCreacion(null, error);
        }

        var cliente = new Cliente
        {
            Id = Guid.NewGuid(),
            Nombre = ReglasCliente.Normalizar(nombre),
            Creado = reloj.GetUtcNow(),
            CreadoPor = usuario.Nombre,
        };
        await clientes.CrearAsync(cliente, ct);
        return new ResultadoCreacion(cliente, null);
    }

    public Task<Cliente?> ObtenerAsync(Guid id, CancellationToken ct = default) => clientes.ObtenerAsync(id, ct);
}
