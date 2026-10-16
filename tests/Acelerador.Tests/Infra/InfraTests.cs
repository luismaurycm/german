using Acelerador.Core.Dominio;
using Acelerador.Infra.Credenciales;
using Acelerador.Infra.Datos;
using Acelerador.Sap;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Infra;

[Trait("HU", "HU-54")]
public class RegistroEjecucionesTests
{
    [Fact(DisplayName = "RNF-Aud-01: el registro guarda usuario, fecha, sistema, paso, objetos y resultado, y sobrevive al reinicio")]
    public async Task PersisteCampos()
    {
        await using var e = await Entorno.CrearAsync();
        var clienteId = Guid.NewGuid();
        var fecha = new DateTimeOffset(2026, 10, 16, 10, 42, 5, TimeSpan.FromHours(-5));
        await e.Registro.RegistrarAsync(new Ejecucion(fecha, @"CM\consultor1", clienteId, "SAP BW", "Probar conexión", "Ninguno (solo lectura)", ResultadoEjecucion.Fallido, "not reached"));
        await e.Registro.RegistrarAsync(new Ejecucion(fecha.AddMinutes(1), @"CM\consultor1", Guid.NewGuid(), "SAP ERP", "Probar conexión", "—", ResultadoEjecucion.Exitoso, "ok"));

        var otraInstancia = new SqliteRegistroEjecuciones(new BaseLocal(e.Base.RutaArchivo));
        var todas = await otraInstancia.ListarAsync();
        var delCliente = Assert.Single(await otraInstancia.ListarAsync(clienteId));

        Assert.Equal(2, todas.Count);
        Assert.True(todas[0].Fecha > todas[1].Fecha, "El registro se lista del más reciente al más antiguo.");
        Assert.Equal(fecha, delCliente.Fecha);
        Assert.Equal(@"CM\consultor1", delCliente.Usuario);
        Assert.Equal("SAP BW", delCliente.Sistema);
        Assert.Equal("Probar conexión", delCliente.Paso);
        Assert.Equal("Ninguno (solo lectura)", delCliente.Objetos);
        Assert.Equal(ResultadoEjecucion.Fallido, delCliente.Resultado);
        Assert.Equal("not reached", delCliente.Mensaje);
        Assert.True(delCliente.Id > 0);
    }

    [Fact(DisplayName = "RNF-Aud-01: el registro solo agrega filas (no expone borrado ni modificación)")]
    public void SoloAgrega()
    {
        var metodos = typeof(Acelerador.Core.Puertos.IRegistroEjecuciones).GetMethods().Select(m => m.Name).ToList();
        Assert.Equal(["RegistrarAsync", "ListarAsync"], metodos);
    }

    [Fact(DisplayName = "ADR-04: el esquema se crea solo y es idempotente; la tabla de conexiones no tiene columna de credencial")]
    public async Task Esquema()
    {
        await using var e = await Entorno.CrearAsync();
        await e.Base.InicializarAsync();

        await using var cn = await e.Base.AbrirAsync();
        await using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT name FROM pragma_table_info('conexiones')";
        var columnas = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            columnas.Add(r.GetString(0));
        }

        Assert.Contains("referencia_credencial", columnas);
        Assert.DoesNotContain(columnas, c => c.Contains("clave", StringComparison.OrdinalIgnoreCase) || c.Contains("password", StringComparison.OrdinalIgnoreCase) || c == "credencial");
    }

    [Fact(DisplayName = "HU-02 CA6 (RNF-Comp-03, ADR-04): la base local vive en el perfil del usuario, sin servidores")]
    public void RutaEnPerfil()
    {
        var ruta = BaseLocal.RutaPorDefecto();
        Assert.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ruta, StringComparison.Ordinal);
        Assert.EndsWith(Path.Combine("CMConsulting", "Acelerador", "acelerador.db"), ruta, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-06 CA4: el cliente guarda y relee arquitectura, versiones, validación y módulos")]
    public async Task ClienteCompleto()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        c.Deteccion = new DeteccionArquitectura(Arquitectura.A, new VersionBw(ProductoBw.Bw4Hana, "2.0"), new VersionBpc(ProductoBpc.Bpc2021, "2021", ModeloBpc.Estandar), "A-BW4H-BPC2021-STD", e.Reloj.Ahora);
        c.Validacion = new ValidacionErpBw(true, e.Reloj.Ahora, "u", "ok");
        c.Modulos.Add(Modulo.ActivosFijos);
        c.AvanzarA(Pasos.Modulos);
        await e.Clientes.ActualizarAsync(c);

        var leido = (await e.Clientes.ObtenerAsync(c.Id))!;

        Assert.Equal(c.Deteccion, leido.Deteccion);
        Assert.Equal(c.Validacion, leido.Validacion);
        Assert.Equal(c.Modulos, leido.Modulos);
        Assert.Equal(Pasos.Modulos, leido.PasoActual);
    }
}

[Trait("HU", "HU-04")]
public class AlmacenCredencialesTests
{
    [Fact(DisplayName = "HU-04: el almacén en memoria guarda, lee y elimina sin tocar el disco")]
    public void Memoria()
    {
        var almacen = new InMemoryCredentialStore();
        almacen.Guardar("ref", "RFC_ACEL", new CredencialSap("s3"));
        Assert.Equal("s3", almacen.Leer("ref")!.Revelar());
        almacen.Eliminar("ref");
        Assert.Null(almacen.Leer("ref"));
        Assert.False(almacen.EsPersistente);
    }

    [WindowsFact(DisplayName = "HU-04 CA2: el Administrador de credenciales de Windows (DPAPI) guarda, lee y elimina")]
    public void Windows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var almacen = new WindowsCredentialStore();
        var referencia = $"CMConsulting.Acelerador/pruebas/{Guid.NewGuid():N}";
        try
        {
            almacen.Guardar(referencia, "RFC_ACEL", new CredencialSap("Cl4ve-Única-ñ"));
            Assert.Equal("Cl4ve-Única-ñ", almacen.Leer(referencia)!.Revelar());
            Assert.True(almacen.EsPersistente);
        }
        finally
        {
            almacen.Eliminar(referencia);
        }

        Assert.Null(almacen.Leer(referencia));
    }
}
