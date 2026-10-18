using System.Globalization;
using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Microsoft.Data.Sqlite;

namespace Acelerador.Infra.Datos;

internal static class Sql
{
    public static object Valor(object? v) => v ?? DBNull.Value;

    public static string Fecha(DateTimeOffset f) => f.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static object Fecha(DateTimeOffset? f) => f is null ? DBNull.Value : Fecha(f.Value);

    public static DateTimeOffset LeerFecha(SqliteDataReader r, int i) =>
        DateTimeOffset.Parse(r.GetString(i), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static DateTimeOffset? LeerFechaNula(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : LeerFecha(r, i);

    public static string? Texto(SqliteDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);

    public static T Enumeracion<T>(string valor)
        where T : struct, Enum => Enum.Parse<T>(valor);

    public static void Parametros(SqliteCommand cmd, params (string Nombre, object? Valor)[] valores)
    {
        foreach (var (nombre, valor) in valores)
        {
            cmd.Parameters.AddWithValue(nombre, Valor(valor));
        }
    }
}

public sealed class SqliteClienteRepository(BaseLocal db) : IClienteRepository
{
    private const string Columnas = """
        id, nombre, creado, creado_por, paso_actual, estado, motivo_estado,
        arquitectura, bw_producto, bw_release, bpc_producto, bpc_release, bpc_modelo, variante, deteccion_fecha,
        validacion_exitosa, validacion_fecha, validacion_usuario, validacion_mensaje, modulos
        """;

    public async Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default)
    {
        await using var cn = await db.AbrirAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM clientes ORDER BY nombre";
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<Cliente>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(Leer(r));
        }

        return lista;
    }

    public async Task<Cliente?> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        await using var cn = await db.AbrirAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $"SELECT {Columnas} FROM clientes WHERE id = $id";
        cmd.Parameters.AddWithValue("$id", id.ToString());
        await using var r = await cmd.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct) ? Leer(r) : null;
    }

    public async Task CrearAsync(Cliente cliente, CancellationToken ct = default)
    {
        await using var cn = await db.AbrirAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO clientes (id, nombre, nombre_clave, creado, creado_por, paso_actual, estado, modulos)
            VALUES ($id, $nombre, $clave, $creado, $creado_por, $paso, $estado, '')
            """;
        Sql.Parametros(cmd,
            ("$id", cliente.Id.ToString()),
            ("$nombre", cliente.Nombre),
            ("$clave", ReglasCliente.Normalizar(cliente.Nombre).ToUpperInvariant()),
            ("$creado", Sql.Fecha(cliente.Creado)),
            ("$creado_por", cliente.CreadoPor),
            ("$paso", cliente.PasoActual),
            ("$estado", cliente.Estado.ToString()));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task ActualizarAsync(Cliente cliente, CancellationToken ct = default)
    {
        var d = cliente.Deteccion;
        var v = cliente.Validacion;
        await using var cn = await db.AbrirAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            UPDATE clientes SET
                paso_actual = $paso, estado = $estado, motivo_estado = $motivo,
                arquitectura = $arq, bw_producto = $bwp, bw_release = $bwr, bpc_producto = $bpcp, bpc_release = $bpcr,
                bpc_modelo = $bpcm, variante = $variante, deteccion_fecha = $dfecha,
                validacion_exitosa = $vok, validacion_fecha = $vfecha, validacion_usuario = $vusuario, validacion_mensaje = $vmensaje,
                modulos = $modulos
            WHERE id = $id
            """;
        Sql.Parametros(cmd,
            ("$id", cliente.Id.ToString()),
            ("$paso", cliente.PasoActual),
            ("$estado", cliente.Estado.ToString()),
            ("$motivo", cliente.MotivoEstado),
            ("$arq", d?.Arquitectura.ToString()),
            ("$bwp", d?.Bw.Producto.ToString()),
            ("$bwr", d?.Bw.Release),
            ("$bpcp", d?.Bpc.Producto.ToString()),
            ("$bpcr", d?.Bpc.Release),
            ("$bpcm", d?.Bpc.Modelo.ToString()),
            ("$variante", d?.VarianteId),
            ("$dfecha", Sql.Fecha(d?.Fecha)),
            ("$vok", v is null ? null : v.Exitosa ? 1 : 0),
            ("$vfecha", Sql.Fecha(v?.Fecha)),
            ("$vusuario", v?.Usuario),
            ("$vmensaje", v?.Mensaje),
            ("$modulos", string.Join(',', cliente.Modulos.OrderBy(m => m))));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    private static Cliente Leer(SqliteDataReader r)
    {
        var modulos = r.GetString(19)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(Sql.Enumeracion<Modulo>)
            .ToHashSet();

        return new Cliente
        {
            Id = Guid.Parse(r.GetString(0)),
            Nombre = r.GetString(1),
            Creado = Sql.LeerFecha(r, 2),
            CreadoPor = r.GetString(3),
            PasoActual = r.GetString(4),
            Estado = Sql.Enumeracion<EstadoCliente>(r.GetString(5)),
            MotivoEstado = Sql.Texto(r, 6),
            Deteccion = r.IsDBNull(7)
                ? null
                : new DeteccionArquitectura(
                    Sql.Enumeracion<Arquitectura>(r.GetString(7)),
                    new VersionBw(Sql.Enumeracion<ProductoBw>(r.GetString(8)), r.GetString(9)),
                    new VersionBpc(Sql.Enumeracion<ProductoBpc>(r.GetString(10)), r.GetString(11), Sql.Enumeracion<ModeloBpc>(r.GetString(12))),
                    Sql.Texto(r, 13),
                    Sql.LeerFecha(r, 14)),
            Validacion = r.IsDBNull(15)
                ? null
                : new ValidacionErpBw(r.GetInt32(15) == 1, Sql.LeerFecha(r, 16), r.GetString(17), r.GetString(18)),
            Modulos = modulos,
        };
    }
}

public sealed class SqliteConexionRepository(BaseLocal db) : IConexionRepository
{
    public async Task<IReadOnlyList<Conexion>> ListarAsync(Guid clienteId, CancellationToken ct = default)
    {
        await using var cn = await db.AbrirAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT sistema, host, mandante, usuario_tecnico, referencia_credencial, estado, mensaje, fecha_prueba, sid, producto
            FROM conexiones WHERE cliente_id = $id
            """;
        cmd.Parameters.AddWithValue("$id", clienteId.ToString());
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<Conexion>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(new Conexion
            {
                ClienteId = clienteId,
                Sistema = Sql.Enumeracion<SistemaSap>(r.GetString(0)),
                Host = r.GetString(1),
                Mandante = r.GetString(2),
                UsuarioTecnico = r.GetString(3),
                ReferenciaCredencial = Sql.Texto(r, 4),
                Estado = Sql.Enumeracion<EstadoPrueba>(r.GetString(5)),
                Mensaje = Sql.Texto(r, 6),
                FechaPrueba = Sql.LeerFechaNula(r, 7),
                Sid = Sql.Texto(r, 8),
                Producto = Sql.Texto(r, 9),
            });
        }

        return lista;
    }

    public async Task GuardarAsync(Conexion c, CancellationToken ct = default)
    {
        await using var cn = await db.AbrirAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO conexiones (cliente_id, sistema, host, mandante, usuario_tecnico, referencia_credencial, estado, mensaje, fecha_prueba, sid, producto)
            VALUES ($cliente, $sistema, $host, $mandante, $usuario, $ref, $estado, $mensaje, $fecha, $sid, $producto)
            ON CONFLICT (cliente_id, sistema) DO UPDATE SET
                host = excluded.host, mandante = excluded.mandante, usuario_tecnico = excluded.usuario_tecnico,
                referencia_credencial = excluded.referencia_credencial, estado = excluded.estado, mensaje = excluded.mensaje,
                fecha_prueba = excluded.fecha_prueba, sid = excluded.sid, producto = excluded.producto
            """;
        Sql.Parametros(cmd,
            ("$cliente", c.ClienteId.ToString()),
            ("$sistema", c.Sistema.ToString()),
            ("$host", c.Host),
            ("$mandante", c.Mandante),
            ("$usuario", c.UsuarioTecnico),
            ("$ref", c.ReferenciaCredencial),
            ("$estado", c.Estado.ToString()),
            ("$mensaje", c.Mensaje),
            ("$fecha", Sql.Fecha(c.FechaPrueba)),
            ("$sid", c.Sid),
            ("$producto", c.Producto));
        await cmd.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>Registro de ejecuciones en SQLite (RNF-Aud-01). Solo inserta y consulta: no borra ni modifica.</summary>
public sealed class SqliteRegistroEjecuciones(BaseLocal db) : IRegistroEjecuciones
{
    public async Task RegistrarAsync(Ejecucion e, CancellationToken ct = default)
    {
        await using var cn = await db.AbrirAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ejecuciones (fecha, usuario, cliente_id, sistema, paso, objetos, resultado, mensaje)
            VALUES ($fecha, $usuario, $cliente, $sistema, $paso, $objetos, $resultado, $mensaje)
            """;
        Sql.Parametros(cmd,
            ("$fecha", Sql.Fecha(e.Fecha)),
            ("$usuario", e.Usuario),
            ("$cliente", e.ClienteId.ToString()),
            ("$sistema", e.Sistema),
            ("$paso", e.Paso),
            ("$objetos", e.Objetos),
            ("$resultado", e.Resultado.ToString()),
            ("$mensaje", e.Mensaje));
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<Ejecucion>> ListarAsync(Guid? clienteId = null, CancellationToken ct = default)
    {
        await using var cn = await db.AbrirAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = """
            SELECT id, fecha, usuario, cliente_id, sistema, paso, objetos, resultado, mensaje
            FROM ejecuciones WHERE $cliente IS NULL OR cliente_id = $cliente ORDER BY fecha DESC, id DESC
            """;
        cmd.Parameters.AddWithValue("$cliente", Sql.Valor(clienteId?.ToString()));
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var lista = new List<Ejecucion>();
        while (await r.ReadAsync(ct))
        {
            lista.Add(new Ejecucion(
                Sql.LeerFecha(r, 1),
                r.GetString(2),
                Guid.Parse(r.GetString(3)),
                r.GetString(4),
                r.GetString(5),
                r.GetString(6),
                Sql.Enumeracion<ResultadoEjecucion>(r.GetString(7)),
                r.GetString(8))
            {
                Id = r.GetInt64(0),
            });
        }

        return lista;
    }
}
