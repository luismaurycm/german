using Acelerador.Core.Dominio;
using Acelerador.Infra.Datos;
using Acelerador.Sap;
using Acelerador.Tests.Soporte;
using Microsoft.Data.Sqlite;

namespace Acelerador.Tests.Infra;

public class EsquemaSprint2Tests
{
    [Fact(DisplayName = "ADR-04: una base del Sprint 1 (esquema 1) se migra al esquema 2 sin perder clientes")]
    public async Task MigraEsquema1()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "acelerador-pruebas", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);
        var ruta = Path.Combine(carpeta, "acelerador.db");
        try
        {
            var id = Guid.NewGuid();
            await using (var cn = new SqliteConnection($"Data Source={ruta}"))
            {
                await cn.OpenAsync();
                await using var cmd = cn.CreateCommand();
                cmd.CommandText = $"""
                    CREATE TABLE esquema (version INTEGER NOT NULL); INSERT INTO esquema VALUES (1);
                    CREATE TABLE clientes (
                        id TEXT PRIMARY KEY, nombre TEXT NOT NULL, nombre_clave TEXT NOT NULL UNIQUE, creado TEXT NOT NULL, creado_por TEXT NOT NULL,
                        paso_actual TEXT NOT NULL, estado TEXT NOT NULL, motivo_estado TEXT, arquitectura TEXT, bw_producto TEXT, bw_release TEXT,
                        bpc_producto TEXT, bpc_release TEXT, bpc_modelo TEXT, variante TEXT, deteccion_fecha TEXT, validacion_exitosa INTEGER,
                        validacion_fecha TEXT, validacion_usuario TEXT, validacion_mensaje TEXT, modulos TEXT NOT NULL DEFAULT '');
                    INSERT INTO clientes (id, nombre, nombre_clave, creado, creado_por, paso_actual, estado, modulos)
                    VALUES ('{id}', 'Grupo Sprint Uno', 'GRUPO SPRINT UNO', '2026-10-16T15:00:00.0000000+00:00', 'consultor1', 'UX-05', 'EnCurso', 'Consolidacion,Presupuesto');
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            SqliteConnection.ClearAllPools();
            var db = new BaseLocal(ruta);
            await db.InicializarAsync();
            await db.InicializarAsync();

            var c = (await new SqliteClienteRepository(db).ObtenerAsync(id))!;
            Assert.Equal("Grupo Sprint Uno", c.Nombre);
            Assert.Equal(VersionPresupuesto.Propuestas, c.Versiones);
            Assert.False(c.TieneSociedadesNoSap);
            await using var cn2 = await db.AbrirAsync();
            await using var v = cn2.CreateCommand();
            v.CommandText = "SELECT version FROM esquema";
            Assert.Equal(2L, await v.ExecuteScalarAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(carpeta, recursive: true);
        }
    }

    [Fact(DisplayName = "HU-07: el inventario se guarda y se lee completo (objetos, bodega, campos Z y fallo)")]
    [Trait("HU", "HU-07")]
    public async Task Inventario()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        var inv = new Inventario
        {
            ClienteId = c.Id,
            Fecha = e.Reloj.Ahora,
            Usuario = "consultor1",
            CamposZIncorporados = new HashSet<string> { "ZZREGION" },
            Fallo = new FalloEscaneo(TipoInventario.ModelosBpc, "sin permiso", e.Reloj.Ahora),
        };
        inv.Objetos[TipoInventario.InfoProviders] = [new("ZACC_C01", "Saldos", true, "InfoCubo", EsBodegaFinanciera: true)];

        await e.Inventarios.GuardarAsync(inv);
        var leido = (await new SqliteInventarioRepository(new BaseLocal(e.Base.RutaArchivo)).ObtenerAsync(c.Id))!;

        Assert.Equal(e.Reloj.Ahora, leido.Fecha);
        Assert.True(leido.BodegaExistente);
        Assert.Equal(inv.Lista(TipoInventario.InfoProviders), leido.Lista(TipoInventario.InfoProviders));
        Assert.Equal(new HashSet<string> { "ZZREGION" }, leido.CamposZIncorporados);
        Assert.Equal(inv.Fallo, leido.Fallo);
    }

    [Fact(DisplayName = "HU-09 y HU-10: las sociedades se guardan en orden con decimales exactos")]
    [Trait("HU", "HU-10")]
    public async Task Sociedades()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        Sociedad[] lista =
        [
            new() { Codigo = "1000", Nombre = "Matriz", MonedaLocal = "COP", PlanCuentas = "PCCO", PorcentajePropiedad = 100m, Metodo = MetodoConsolidacion.Matriz },
            new() { Codigo = "9000", Nombre = "No SAP", EnSap = false, MonedaLocal = "COP", Matriz = "1000", PorcentajePropiedad = 33.3333m, Metodo = MetodoConsolidacion.Participacion },
        ];

        await e.Sociedades.GuardarAsync(c.Id, lista);
        await e.Sociedades.GuardarAsync(c.Id, lista);
        var leidas = await e.Sociedades.ListarAsync(c.Id);

        Assert.Equal(["1000", "9000"], leidas.Select(s => s.Codigo));
        Assert.Equal(33.3333m, leidas[1].PorcentajePropiedad);
        Assert.False(leidas[1].EnSap);
        Assert.Equal(MetodoConsolidacion.Participacion, leidas[1].Metodo);
    }

    [Fact(DisplayName = "HU-11: el mapeo se guarda por plan de cuentas, se reasigna y se quita")]
    [Trait("HU", "HU-11")]
    public async Task Mapeo()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();

        await e.Mapeos.GuardarAsync(c.Id, "PCCO", ["1105", "1110"], "F-00-101");
        await e.Mapeos.GuardarAsync(c.Id, "PCCO", ["1110"], "F-00-102");
        await e.Mapeos.GuardarAsync(c.Id, "PCPA", ["1010"], "F-00-101");
        await e.Mapeos.GuardarAsync(c.Id, "PCCO", ["1105"], null);

        Assert.Equal([new AsignacionCuenta("PCCO", "1110", "F-00-102")], await e.Mapeos.ListarAsync(c.Id, "PCCO"));
        Assert.Single(await e.Mapeos.ListarAsync(c.Id, "PCPA"));
    }
}
