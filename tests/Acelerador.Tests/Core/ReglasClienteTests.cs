using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-02")]
public class ReglasClienteTests
{
    private static readonly string[] Existentes = ["Grupo Ejemplo Andino", "Holding Ejemplo Caribe"];

    [Theory(DisplayName = "HU-02 CA2: el nombre es obligatorio")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NombreObligatorio(string? nombre) =>
        Assert.Equal("El nombre del cliente es obligatorio.", ReglasCliente.ValidarNombre(nombre, Existentes));

    [Theory(DisplayName = "HU-02 CA2: el nombre no puede repetir uno existente (sin distinguir mayúsculas, tildes ni espacios)")]
    [InlineData("Grupo Ejemplo Andino")]
    [InlineData("grupo ejemplo andino")]
    [InlineData("  Grupo   Ejemplo  Andino ")]
    [InlineData("Grupo Ejemplo Andinó")]
    public void NombreNoRepetido(string nombre) =>
        Assert.Equal("Ya existe un cliente con el nombre «" + ReglasCliente.Normalizar(nombre) + "».", ReglasCliente.ValidarNombre(nombre, Existentes));

    [Fact(DisplayName = "HU-02 CA2: un nombre nuevo es válido")]
    public void NombreNuevoValido() => Assert.Null(ReglasCliente.ValidarNombre("Corporación Ejemplo Sur", Existentes));

    [Fact(DisplayName = "HU-02 CA2: el nombre tiene un largo máximo")]
    public void NombreLargo() => Assert.NotNull(ReglasCliente.ValidarNombre(new string('x', ReglasCliente.LongitudMaxima + 1), []));

    [Theory(DisplayName = "HU-02 CA5: la búsqueda filtra por nombre sin distinguir mayúsculas ni tildes")]
    [InlineData("caribe", new[] { "Holding Ejemplo Caribe" })]
    [InlineData("CORPORACION", new[] { "Corporación Ejemplo Sur" })]
    [InlineData("ejemplo", new[] { "Corporación Ejemplo Sur", "Grupo Ejemplo Andino", "Holding Ejemplo Caribe" })]
    [InlineData("", new[] { "Corporación Ejemplo Sur", "Grupo Ejemplo Andino", "Holding Ejemplo Caribe" })]
    [InlineData("norte", new string[0])]
    public void Buscar(string q, string[] esperados)
    {
        var clientes = new[] { "Holding Ejemplo Caribe", "Grupo Ejemplo Andino", "Corporación Ejemplo Sur" }
            .Select(n => new Cliente { Id = Guid.NewGuid(), Nombre = n, Creado = DateTimeOffset.UnixEpoch, CreadoPor = "x" });

        Assert.Equal(esperados, ReglasCliente.Filtrar(clientes, q).Select(c => c.Nombre));
    }

    [Fact(DisplayName = "Flujo: el paso actual solo avanza, nunca retrocede")]
    public void PasoSoloAvanza()
    {
        var c = new Cliente { Id = Guid.NewGuid(), Nombre = "X", Creado = DateTimeOffset.UnixEpoch, CreadoPor = "x" };
        c.AvanzarA(Pasos.Modulos);
        c.AvanzarA(Pasos.Diagnostico);
        Assert.Equal(Pasos.Modulos, c.PasoActual);
    }
}
