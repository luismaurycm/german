using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-12")]
public class VersionesTests
{
    private static List<VersionPresupuesto> Propuestas() => [.. VersionPresupuesto.Propuestas];

    [Fact(DisplayName = "HU-12 CA1: se proponen presupuesto anual y las proyecciones 3+9, 6+6 y 9+3")]
    public void Propuestas_()
    {
        Assert.Equal(["Presupuesto anual", "Proyección 3+9", "Proyección 6+6", "Proyección 9+3"], VersionPresupuesto.Propuestas.Select(v => v.Nombre));
        Assert.Single(VersionPresupuesto.Propuestas, v => v.Anual);
        Assert.Empty(ReglasVersiones.Validar(Propuestas()));
    }

    [Fact(DisplayName = "HU-12 CA1: se pueden agregar, renombrar y quitar versiones")]
    public async Task AgregarRenombrarQuitar()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        var versiones = Propuestas();
        versiones.Add(new VersionPresupuesto(" v2 ", " Versión 2 "));
        versiones[1] = versiones[1] with { Nombre = "Reproyección 3+9" };
        versiones.RemoveAll(v => v.Codigo == "PROY_9_3");

        var r = await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Presupuesto], continuar: false, versiones);

        Assert.True(r.Guardado);
        var guardadas = (await e.Clientes.ObtenerAsync(c.Id))!.Versiones;
        Assert.Equal(["PRESUP_ANUAL", "PROY_3_9", "PROY_6_6", "V2"], guardadas.Select(v => v.Codigo));
        Assert.Equal("Reproyección 3+9", guardadas[1].Nombre);
        Assert.Equal("Versión 2", guardadas[3].Nombre);
    }

    [Fact(DisplayName = "HU-12 CA2: debe existir la versión de presupuesto anual")]
    public async Task AnualObligatoria()
    {
        var sinAnual = Propuestas().Where(v => !v.Anual).ToList();
        Assert.Equal(ReglasVersiones.MensajeSinAnual, Assert.Single(ReglasVersiones.Validar(sinAnual)).Mensaje);

        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        var r = await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Presupuesto], continuar: true, sinAnual);

        Assert.False(r.Guardado);
        Assert.False(r.Avanzo);
        Assert.Equal(ReglasVersiones.MensajeSinAnual, Assert.Single(r.ErroresVersiones!).Mensaje);
        Assert.Equal(4, (await e.Clientes.ObtenerAsync(c.Id))!.Versiones.Count);
        Assert.Equal(Pasos.Modulos, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);
    }

    [Fact(DisplayName = "HU-12 CA3: los códigos de versión no se repiten (sin distinguir mayúsculas ni espacios)")]
    public void CodigosUnicos()
    {
        var versiones = Propuestas();
        versiones.Add(new VersionPresupuesto(" proy_6_6", "Otra 6+6"));

        var e = Assert.Single(ReglasVersiones.Validar(ReglasVersiones.Normalizar(versiones)));

        Assert.Equal(4, e.Fila);
        Assert.Equal("El código de versión PROY_6_6 está repetido.", e.Mensaje);
    }

    [Theory(DisplayName = "HU-12: el código es obligatorio, sin espacios ni símbolos, y la versión lleva nombre")]
    [InlineData("", "Nueva", "Escribe el código de la versión de la fila 5.")]
    [InlineData("PROY 2", "Nueva", "El código PROY 2 solo admite letras, números y guion bajo (hasta 20).")]
    [InlineData("PROY_2", "", "La versión PROY_2 no tiene nombre.")]
    public void CodigoYNombre(string codigo, string nombre, string mensaje)
    {
        var versiones = Propuestas();
        versiones.Add(new VersionPresupuesto(codigo, nombre));

        Assert.Equal(mensaje, Assert.Single(ReglasVersiones.Validar(ReglasVersiones.Normalizar(versiones))).Mensaje);
    }

    [Fact(DisplayName = "HU-12 CA4: las versiones pasan al plan de generación solo con el módulo Presupuesto")]
    public async Task PasanAlPlan()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();

        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion, Modulo.Presupuesto], continuar: false, Propuestas());
        Assert.Equal(VersionPresupuesto.Propuestas, ReglasVersiones.ParaPlan((await e.Clientes.ObtenerAsync(c.Id))!));

        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: false, Propuestas().Take(1).ToList());
        var sinPresupuesto = (await e.Clientes.ObtenerAsync(c.Id))!;
        Assert.Empty(ReglasVersiones.ParaPlan(sinPresupuesto));
        Assert.Equal(4, sinPresupuesto.Versiones.Count);
    }
}
