using Microsoft.Data.Sqlite;

namespace Acelerador.Infra.Datos;

/// <summary>
/// Base SQLite local en el perfil del usuario (ADR-04): clientes, conexiones sin credencial, inventario,
/// sociedades, mapeo de cuentas, ejecuciones del plan y registro de ejecuciones. Sin servicios de base de datos (RNF-Comp-03).
/// </summary>
public sealed class BaseLocal
{
    public const int VersionEsquema = 4;

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

    /// <summary>Crea el archivo y el esquema si no existen, y migra una base del Sprint 1 (versión 1). Idempotente.</summary>
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
            CREATE INDEX IF NOT EXISTS ix_ejecuciones_fecha ON ejecuciones (fecha);

            -- Último inventario del cliente (HU-07): objetos en JSON, campos Z elegidos y el fallo del último intento.
            CREATE TABLE IF NOT EXISTS inventarios (
                cliente_id     TEXT PRIMARY KEY REFERENCES clientes(id),
                fecha          TEXT,
                usuario        TEXT,
                objetos        TEXT NOT NULL,
                campos_z       TEXT NOT NULL DEFAULT '',
                fallo_tipo     TEXT,
                fallo_mensaje  TEXT,
                fallo_fecha    TEXT
            );

            -- Sociedades del grupo (HU-09, HU-10). El porcentaje va como texto invariante para no perder decimales.
            CREATE TABLE IF NOT EXISTS sociedades (
                cliente_id     TEXT NOT NULL REFERENCES clientes(id),
                orden          INTEGER NOT NULL,
                codigo         TEXT NOT NULL,
                nombre         TEXT NOT NULL,
                en_sap         INTEGER NOT NULL,
                moneda         TEXT NOT NULL,
                matriz         TEXT NOT NULL,
                plan_cuentas   TEXT NOT NULL,
                porcentaje     TEXT NOT NULL,
                metodo         TEXT NOT NULL,
                PRIMARY KEY (cliente_id, codigo)
            );

            -- Mapeo de cuentas a rubros por plan de cuentas (HU-11, RN-05).
            CREATE TABLE IF NOT EXISTS mapeo_cuentas (
                cliente_id     TEXT NOT NULL REFERENCES clientes(id),
                plan_cuentas   TEXT NOT NULL,
                cuenta         TEXT NOT NULL,
                rubro          TEXT NOT NULL,
                PRIMARY KEY (cliente_id, plan_cuentas, cuenta)
            );

            -- Ejecuciones del plan de generación (HU-14): pasos, estado y objetos en JSON.
            CREATE TABLE IF NOT EXISTS ejecuciones_plan (
                id          TEXT PRIMARY KEY,
                cliente_id  TEXT NOT NULL REFERENCES clientes(id),
                inicio      TEXT NOT NULL,
                fin         TEXT,
                datos       TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_ejecuciones_plan_cliente ON ejecuciones_plan (cliente_id, inicio);

            -- Cargas confirmadas de sociedades no SAP (HU-25). Solo metadatos: las filas quedan en BW.
            CREATE TABLE IF NOT EXISTS cargas_no_sap (
                cliente_id  TEXT NOT NULL REFERENCES clientes(id),
                sociedad    TEXT NOT NULL,
                periodo     TEXT NOT NULL,
                archivo     TEXT NOT NULL,
                filas       INTEGER NOT NULL,
                total       TEXT NOT NULL,
                fecha       TEXT NOT NULL,
                usuario     TEXT NOT NULL,
                PRIMARY KEY (cliente_id, sociedad, periodo)
            );
            """;
        await cmd.ExecuteNonQueryAsync(ct);

        // Versión 2 (Sprint 2): versiones de presupuesto (HU-12) y marca de sociedades no SAP (HU-09 CA5).
        await AgregarColumnaAsync(cn, "clientes", "versiones", "TEXT", ct);
        await AgregarColumnaAsync(cn, "clientes", "sociedades_no_sap", "INTEGER NOT NULL DEFAULT 0", ct);

        await using var version = cn.CreateCommand();
        version.CommandText = $"UPDATE esquema SET version = {VersionEsquema} WHERE version < {VersionEsquema}";
        await version.ExecuteNonQueryAsync(ct);
    }

    private static async Task AgregarColumnaAsync(SqliteConnection cn, string tabla, string columna, string tipo, CancellationToken ct)
    {
        await using var existe = cn.CreateCommand();
        existe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tabla}') WHERE name = '{columna}'";
        if (Convert.ToInt64(await existe.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture) > 0)
        {
            return;
        }

        await using var alter = cn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {tabla} ADD COLUMN {columna} {tipo}";
        await alter.ExecuteNonQueryAsync(ct);
    }
}
