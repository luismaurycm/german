using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

public class ServicioClientesTests
{
    [Fact(DisplayName = "HU-02 CA1: la lista trae arquitectura, paso, estado, última ejecución y usuario")]
    [Trait("HU", "HU-02")]
    public async Task ListaConDatos()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ClienteAsync("Holding Ejemplo Caribe");
        e.Reloj.Ahora = e.Reloj.Ahora.AddHours(2);
        await e.ConectarTodoAsync(c.Id);

        var lista = await e.ServicioClientes.ListarAsync(null);

        Assert.Equal(2, lista.Total);
        var fila = lista.Filas.Single(f => f.Cliente.Id == c.Id);
        Assert.Equal(Arquitectura.A, fila.Cliente.Deteccion!.Arquitectura);
        Assert.Equal(Pasos.Conexiones, fila.Cliente.PasoActual);
        Assert.Equal(EstadoCliente.EnCurso, fila.Cliente.Estado);
        Assert.Equal(e.Reloj.Ahora, fila.Fecha);
        Assert.Equal(e.Usuario.Nombre, fila.Usuario);

        var sinEjecuciones = lista.Filas.Single(f => f.Cliente.Id != c.Id);
        Assert.Null(sinEjecuciones.Fecha);
        Assert.Equal(e.Usuario.Nombre, sinEjecuciones.Usuario);
    }

    [Fact(DisplayName = "HU-02 CA2: crear un cliente repetido devuelve el error y no lo crea")]
    [Trait("HU", "HU-02")]
    public async Task NoRepetido()
    {
        await using var e = await Entorno.CrearAsync();
        await e.ClienteAsync();

        var r = await e.ServicioClientes.CrearAsync("GRUPO EJEMPLO ANDINO");

        Assert.Null(r.Cliente);
        Assert.Equal("Ya existe un cliente con el nombre «GRUPO EJEMPLO ANDINO».", r.Error);
        Assert.Equal(1, (await e.ServicioClientes.ListarAsync(null)).Total);
    }

    [Fact(DisplayName = "HU-02 CA3: un cliente nuevo empieza en Conexiones (UX-02), en curso")]
    [Trait("HU", "HU-02")]
    public async Task NuevoEmpiezaEnConexiones()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync("  Corporación   Ejemplo Sur ");

        Assert.Equal("Corporación Ejemplo Sur", c.Nombre);
        Assert.Equal(Pasos.Conexiones, c.PasoActual);
        Assert.Equal(EstadoCliente.EnCurso, c.Estado);
        Assert.Equal(e.Usuario.Nombre, c.CreadoPor);
    }

    [Fact(DisplayName = "HU-02 CA5: la búsqueda filtra la lista por nombre y conserva el total")]
    [Trait("HU", "HU-02")]
    public async Task Busqueda()
    {
        await using var e = await Entorno.CrearAsync();
        await e.ClienteAsync();
        await e.ClienteAsync("Holding Ejemplo Caribe");

        var lista = await e.ServicioClientes.ListarAsync("caribe");

        Assert.Equal(2, lista.Total);
        Assert.Equal("Holding Ejemplo Caribe", Assert.Single(lista.Filas).Cliente.Nombre);
    }
}

[Trait("HU", "HU-08")]
public class ServicioModulosTests
{
    private static async Task<(Entorno, Cliente)> ClienteEnModulosAsync()
    {
        var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        return (e, c);
    }

    [Fact(DisplayName = "HU-08: la selección se guarda en el cliente")]
    public async Task Guarda()
    {
        var (e, c) = await ClienteEnModulosAsync();
        await using var _ = e;

        var r = await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion, Modulo.Deterioros], continuar: false);

        Assert.True(r.Guardado);
        Assert.False(r.Avanzo);
        Assert.Equal(new HashSet<Modulo> { Modulo.Consolidacion, Modulo.Deterioros }, (await e.Clientes.ObtenerAsync(c.Id))!.Modulos);
    }

    [Fact(DisplayName = "HU-08 CA3: continuar sin módulos devuelve «Elige al menos un módulo.» y no avanza")]
    public async Task ContinuarSinModulos()
    {
        var (e, c) = await ClienteEnModulosAsync();
        await using var _ = e;

        var r = await e.ServicioModulos.GuardarAsync(c.Id, [], continuar: true);

        Assert.False(r.Avanzo);
        Assert.Equal(ReglasModulos.MensajeSinModulos, r.Error);
        Assert.Equal(Pasos.Modulos, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);
    }

    [Fact(DisplayName = "HU-08 CA3: con al menos un módulo, continuar avanza a Sociedades")]
    public async Task ContinuarConModulos()
    {
        var (e, c) = await ClienteEnModulosAsync();
        await using var _ = e;

        var r = await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Presupuesto], continuar: true);

        Assert.True(r.Avanzo);
        Assert.Equal(Pasos.Sociedades, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);
    }

    [Fact(DisplayName = "Flujo: del diagnóstico solo se pasa a módulos si el cliente llegó al diagnóstico")]
    public async Task NoSaltaPasos()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp));

        Assert.False((await e.ServicioDiagnostico.GuardarCamposZAsync(c.Id, [], continuar: true)).Exitosa);
        Assert.Equal(Pasos.Conexiones, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);
    }
}
