using Microsoft.Data.Sqlite;

namespace Acelerador.Infra.Datos;

/// <summary>
/// Base SQLite local en el perfil del usuario (ADR-04): clientes, conexiones sin credencial y
/// registro de ejecuciones. Sin servicios de base de datos (RNF-Comp-03).
/// </summary>
public sealed class BaseLocal
{
    public const int VersionEsquema = 1;

    private readonly string _cadena;

    public BaseLocal(string rutaArchivo)
    {
        RutaArchivo = Path.GetFullPath(rutaArchivo);
        _cadena = new SqliteConnectionStringBuilder
        {
            DataSource = RutaArchivo,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        }.ToString();
    }

    public string RutaArchivo { get; }

    /// <summary>%LOCALAPPDATA%\CMConsulting\Acelerador\acelerador.db (o su equivalente fuera de Windows).</summary>
    public static string RutaPorDefecto() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CMConsulting", "Acelerador", "acelerador.db");

    public async Task<SqliteConnection> AbrirAsync(CancellationToken ct = default)
    {
        var cn = new SqliteConnection(_cadena);
        await cn.OpenAsync(ct);
        return cn;
    }

    /// <summary>Crea el archivo y el esquema si no existen. Idempotente.</summary>
    public async Task InicializarAsync(CancellationToken ct = default)
    {
        var carpeta = Path.GetDirectoryName(RutaArchivo);
        if (!string.IsNullOrEmpty(carpeta))
        {
            Directory.CreateDirectory(carpeta);
        }

        await using var cn = await AbrirAsync(ct);
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = $"""
            CREATE TABLE IF NOT EXISTS esquema (version INTEGER NOT NULL);
            INSERT INTO esquema (version) SELECT {VersionEsquema} WHERE NOT EXISTS (SELECT 1 FROM esquema);

            CREATE TABLE IF NOT EXISTS clientes (
                id                  TEXT PRIMARY KEY,
                nombre              TEXT NOT NULL,
                nombre_clave        TEXT NOT NULL UNIQUE,
                creado              TEXT NOT NULL,
                creado_por          TEXT NOT NULL,
                paso_actual         TEXT NOT NULL,
                estado              TEXT NOT NULL,
                motivo_estado       TEXT,
                arquitectura        TEXT,
                bw_producto         TEXT,
                bw_release          TEXT,
                bpc_producto        TEXT,
                bpc_release         TEXT,
                bpc_modelo          TEXT,
                variante            TEXT,
                deteccion_fecha     TEXT,
                validacion_exitosa  INTEGER,
                validacion_fecha    TEXT,
                validacion_usuario  TEXT,
                validacion_mensaje  TEXT,
                modulos             TEXT NOT NULL DEFAULT ''
            );

            -- Sin columna de credencial: solo la referencia al almacén de Windows (RNF-Seg-02).
            CREATE TABLE IF NOT EXISTS conexiones (
                cliente_id            TEXT NOT NULL REFERENCES clientes(id),
                sistema               TEXT NOT NULL,
                host                  TEXT NOT NULL,
                mandante              TEXT NOT NULL,
                usuario_tecnico       TEXT NOT NULL,
                referencia_credencial TEXT,
                estado                TEXT NOT NULL,
                mensaje               TEXT,
                fecha_prueba          TEXT,
                sid                   TEXT,
                producto              TEXT,
                PRIMARY KEY (cliente_id, sistema)
            );

            -- Registro de ejecuciones (RNF-Aud-01). Solo se agregan filas; la retención mínima es de 12 meses.
            CREATE TABLE IF NOT EXISTS ejecuciones (
                id          INTEGER PRIMARY KEY AUTOINCREMENT,
                fecha       TEXT NOT NULL,
                usuario     TEXT NOT NULL,
                cliente_id  TEXT NOT NULL,
                sistema     TEXT NOT NULL,
                paso        TEXT NOT NULL,
                objetos     TEXT NOT NULL,
                resultado   TEXT NOT NULL,
                mensaje     TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_ejecuciones_cliente ON ejecuciones (cliente_id, fecha);
            """;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
