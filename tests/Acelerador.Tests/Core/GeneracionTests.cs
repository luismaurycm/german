using System.Text.Json;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

public class ReglasCamposZTests
{
    [Theory(DisplayName = "HU-17 CA3: solo los tipos compatibles con un InfoObject se incorporan")]
    [Trait("HU", "HU-17")]
    [InlineData("CHAR 4", true)]
    [InlineData("CHAR 60", true)]
    [InlineData("NUMC 10", true)]
    [InlineData("DATS", true)]
    [InlineData("CUKY", true)]
    [InlineData("CHAR 61", false)]
    [InlineData("STRG", false)]
    [InlineData("RSTR", false)]
    [InlineData("LCHR", false)]
    [InlineData(null, false)]
    public void Compatibilidad(string? tipo, bool compatible) => Assert.Equal(compatible, ReglasCamposZ.Compatible(tipo));

    [Fact(DisplayName = "HU-17 CA3: el motivo dice por qué no se incorpora")]
    [Trait("HU", "HU-17")]
    public void Motivo()
    {
        Assert.Equal("Tipo STRG no compatible con un InfoObject de BW (solo CHAR, NUMC, DATS, TIMS, CUKY o UNIT).", ReglasCamposZ.MotivoIncompatible("STRG"));
        Assert.Equal("Tipo CHAR 80 no compatible: un InfoObject admite hasta 60 caracteres.", ReglasCamposZ.MotivoIncompatible("CHAR 80"));
    }
}

public class PlanGeneracionTests
{
    [Fact(DisplayName = "HU-13 CA1: el plan agrupa SAP BW, SAP BPC y Monedas y tasas, y marca cada objeto Nuevo o Existente")]
    [Trait("HU", "HU-13")]
    public async Task AgrupaYMarca()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        Assert.True(plan.Listo);
        Assert.Equal(CatalogoVariantes.Bw75Bpc101, plan.Variante);
        Assert.All(plan.DeCapa(Capa.Bw), o => Assert.Contains(o.Estado, new[] { EstadoObjeto.Nuevo, EstadoObjeto.Existente }));
        Assert.Contains(plan.Objetos, o => o.NombreTecnico == "0FI_GL_14" && o.Sistema == SistemaSap.Erp && o.Estado == EstadoObjeto.Existente);
        Assert.Contains(plan.Objetos, o => o.NombreTecnico == GeneradorBodegaBw75.CuboSaldos && o.Estado == EstadoObjeto.Nuevo);
        Assert.Equal([("1000", "COP"), ("2000", "COP"), ("3000", "USD")], plan.Monedas.MonedasLocales);
        Assert.Equal(["Cierre", "Promedio", "Histórica"], plan.Monedas.Tasas.Select(t => t.Tipo));
        Assert.Equal(plan.Objetos.Count(o => o.Estado == EstadoObjeto.Nuevo), plan.TotalNuevos);
    }

    [Fact(DisplayName = "HU-13 CA2 y HU-15 CA2: con bodega existente sus objetos quedan Existente y el paso no crea nada (RN-03)")]
    [Trait("HU", "HU-13")]
    public async Task BodegaExistente()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.BodegaExistente = true;
        var c = await e.ClienteEnPlanAsync();

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        var paso = plan.Pasos.Single(p => p.Id == PasosGeneracion.ObjetosBw);
        Assert.True(paso.SoloExistentes);
        Assert.Equal(0, paso.Nuevos);
        Assert.All(paso.Objetos, o => Assert.Equal(EstadoObjeto.Existente, o.Estado));
    }

    [Fact(DisplayName = "HU-15 CA1: sin bodega se activa 0FI_GL_14 y se crean el DSO, los InfoCubos, transformaciones y DTP")]
    [Trait("HU", "HU-15")]
    public async Task BodegaNueva()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();

        var objetos = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan.Objetos.ToList();

        Assert.All(objetos, o => Assert.Equal(EstadoObjeto.Nuevo, o.Estado));
        Assert.Contains(objetos, o => o is { Sistema: SistemaSap.Erp, NombreTecnico: "0FI_GL_14" } && o.Operacion.Funcion == FuncionesZ.Activar);
        Assert.Equal(["DSO", "InfoCubo", "InfoCubo"], objetos.Where(o => o.Tipo is "DSO" or "InfoCubo").Select(o => o.Tipo));
        Assert.Equal(3, objetos.Count(o => o.Tipo == "Transformación"));
        Assert.Equal(3, objetos.Count(o => o.Tipo == "DTP"));
        Assert.DoesNotContain(objetos, o => o.Tipo == "aDSO");
        var cubo = JsonDocument.Parse(objetos.First(o => o.NombreTecnico == GeneradorBodegaBw75.CuboSaldos).Operacion.Definicion).RootElement;
        Assert.Equal("INFOCUBO", cubo.GetProperty("tipo").GetString());
        Assert.Contains("0PCOMPANY", cubo.GetProperty("campos").GetProperty("caracteristicas").EnumerateArray().Select(x => x.GetString()));
    }

    [Fact(DisplayName = "HU-17 CA2 y CA3: los campos Z marcados aparecen como Nuevo; uno con tipo incompatible queda No incorporable con el motivo")]
    [Trait("HU", "HU-17")]
    public async Task CamposZEnElPlan()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        var inv = (await e.Inventarios.ObtenerAsync(c.Id))!;
        inv.CamposZIncorporados = new HashSet<string> { "ZZREGION", "ZZNOTAS" };
        await e.Inventarios.GuardarAsync(inv);
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo().Take(3).ToList(), continuar: true);
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501"]);

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        var region = plan.Objetos.Single(o => o.Grupo == GeneradorCamposZ.Grupo && o.NombreTecnico == "ZZREGION");
        var notas = plan.Objetos.Single(o => o.Grupo == GeneradorCamposZ.Grupo && o.NombreTecnico == "ZZNOTAS");
        Assert.Equal(EstadoObjeto.Nuevo, region.Estado);
        Assert.Equal("InfoObject", region.Tipo);
        Assert.Equal(EstadoObjeto.NoIncorporable, notas.Estado);
        Assert.StartsWith("Tipo STRG no compatible", notas.Nota, StringComparison.Ordinal);
        var dso = JsonDocument.Parse(plan.Objetos.First(o => o.NombreTecnico == GeneradorBodegaBw75.Dso).Operacion.Definicion).RootElement;
        var campos = dso.GetProperty("campos").GetProperty("caracteristicas").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("ZZREGION", campos);
        Assert.DoesNotContain("ZZNOTAS", campos);
    }

    [Fact(DisplayName = "HU-17 CA3: UX-03 no deja incorporar un campo Z incompatible aunque llegue marcado")]
    [Trait("HU", "HU-17")]
    public async Task NoIncorporaIncompatible()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();

        await e.ServicioDiagnostico.GuardarCamposZAsync(c.Id, ["ZZREGION", "ZZNOTAS"], continuar: false);

        Assert.Equal(new HashSet<string> { "ZZREGION" }, (await e.Inventarios.ObtenerAsync(c.Id))!.CamposZIncorporados);
    }

    [Fact(DisplayName = "HU-13 CA4: si falta un paso anterior el plan dice cuál y en qué pantalla se completa")]
    [Trait("HU", "HU-13")]
    public async Task Faltantes()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo().Take(3).ToList(), continuar: true);

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        Assert.False(plan.Listo);
        Assert.Empty(plan.Pasos);
        var f = Assert.Single(plan.Faltantes);
        Assert.Equal(Pasos.Mapeo, f.Pantalla);
        Assert.Equal("Mapeo de cuentas: aún no hay cuentas mapeadas a los rubros.", f.Mensaje);
    }

    [Fact(DisplayName = "HU-13 CA4: un escaneo fallido lleva al diagnóstico; sin módulos ni sociedades, a sus pantallas")]
    [Trait("HU", "HU-13")]
    public void FaltantesEnOrden()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "X", Creado = DateTimeOffset.UtcNow, CreadoPor = "u" };
        cliente.Deteccion = new DeteccionArquitectura(Arquitectura.A, new(ProductoBw.Bw75, "7.5"), new(ProductoBpc.Bpc101, "10.1", ModeloBpc.Estandar), CatalogoVariantes.Bw75Bpc101.Id, DateTimeOffset.UtcNow);
        var inv = new Inventario { ClienteId = cliente.Id, Fecha = DateTimeOffset.UtcNow, Fallo = new FalloEscaneo(TipoInventario.ModelosBpc, "x", DateTimeOffset.UtcNow) };

        var faltan = GeneradorPlan.Faltantes(cliente, inv, [], 0);

        Assert.Equal([Pasos.Diagnostico, Pasos.Modulos, Pasos.Sociedades], faltan.Select(f => f.Pantalla));
        Assert.Equal("Diagnóstico: " + ReglasInventario.MensajeEscaneoFallido, faltan[0].Mensaje);
    }

    [Fact(DisplayName = "RN-19: un cliente no soportado no tiene plan")]
    [Trait("HU", "HU-13")]
    public void NoSoportado()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "X", Creado = DateTimeOffset.UtcNow, CreadoPor = "u", Estado = EstadoCliente.NoSoportado, MotivoEstado = "Arquitectura B no soportada en la versión 1 (RN-19)." };

        var plan = GeneradorPlan.Predeterminado.Armar(cliente, null, [], []);

        Assert.Equal(Pasos.Conexiones, Assert.Single(plan.Faltantes).Pantalla);
    }

    [Fact(DisplayName = "RNF-Comp-04: el plan se arma con los generadores del catálogo; uno nuevo se agrega sin tocar los existentes")]
    [Trait("HU", "HU-13")]
    public async Task GeneradoresExtensibles()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();
        var generador = new GeneradorPlan([.. GeneradorPlan.VersionUno, new GeneradorDePrueba()], CatalogoVariantes.VersionUno);
        var servicio = new Acelerador.Core.Servicios.ServicioPlan(e.Clientes, e.Inventarios, e.Sociedades, e.Mapeos, e.Ejecuciones, e.Cargas, generador, e.Reloj);

        var plan = (await servicio.ObtenerAsync(c.Id))!.Plan;

        Assert.Equal(PasosGeneracion.ObjetosBw, plan.Pasos[0].Id);
        Assert.Equal(PasosGeneracion.Conversion, plan.Pasos[^1].Id);
        Assert.Equal("ZPRUEBA", Assert.Single(plan.Pasos[^1].Objetos).NombreTecnico);
    }

    private sealed class GeneradorDePrueba : IGeneradorObjetos
    {
        public bool Aplica(ContextoPlan contexto) => true;

        public IEnumerable<ObjetoPlan> Generar(ContextoPlan contexto) =>
            [new(PasosGeneracion.Conversion, Capa.Bpc, "Prueba", SistemaSap.Bpc, "Script", "ZPRUEBA", "Prueba", EstadoObjeto.Nuevo, new("Z", "SCRIPT", "ZPRUEBA", "{}"))];
    }
}
