using System.Text;
using System.Text.Json;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

public class VarianteBw4HanaTests
{
    [Fact(DisplayName = "HU-16 CA1 y CA2: con BW/4HANA el plan usa la variante BW/4HANA, crea aDSO y ningún InfoCubo")]
    [Trait("HU", "HU-16")]
    public async Task AdsoSinInfoCubos()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.ABw4Hana);
        var c = await e.ClienteEnPlanAsync();

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        Assert.Equal(CatalogoVariantes.Bw4HanaBpc2021, plan.Variante);
        Assert.Contains(plan.Objetos, o => o is { Tipo: "aDSO", NombreTecnico: GeneradorBodegaBw4Hana.AdsoPartidas });
        Assert.Contains(plan.Objetos, o => o is { Tipo: "aDSO", NombreTecnico: GeneradorBodegaBw4Hana.AdsoSaldos });
        Assert.DoesNotContain(plan.Objetos, o => o.Tipo == "InfoCubo" || o.Operacion.TipoObjeto == "INFOCUBO");
        Assert.DoesNotContain(plan.Objetos, o => o.Definicion().Contains("INFOCUBO", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "HU-16 CA1: la variante sale de la detección; la configuración del cliente no cambia al generar (RNF-Comp-04)")]
    [Trait("HU", "HU-16")]
    public async Task SinCambiarConfiguracion()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.ABw4Hana);
        var c = await e.ClienteEnPlanAsync();
        var antes = await e.Clientes.ObtenerAsync(c.Id);
        var sociedades = JsonSerializer.Serialize(await e.Sociedades.ListarAsync(c.Id));

        var ej = await e.GenerarAsync(c.Id);

        var despues = (await e.Clientes.ObtenerAsync(c.Id))!;
        Assert.Equal(CatalogoVariantes.Bw4HanaBpc2021.Id, ej.VarianteId);
        Assert.Equal(antes!.Modulos, despues.Modulos);
        Assert.Equal(antes.Deteccion, despues.Deteccion);
        Assert.Equal(antes.Versiones, despues.Versiones);
        Assert.Equal(sociedades, JsonSerializer.Serialize(await e.Sociedades.ListarAsync(c.Id)));
        Assert.DoesNotContain(e.Sap.Objetos, o => o.Operacion.TipoObjeto == "INFOCUBO");
        Assert.Contains(e.Sap.Objetos, o => o.Operacion.TipoObjeto == "ADSO");
    }

    [Fact(DisplayName = "HU-16 CA3: en S/4HANA el extractor sobre ACDOCA se conecta por ODP")]
    [Trait("HU", "HU-16")]
    public async Task Odp()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.ABw4Hana);
        var c = await e.ClienteEnPlanAsync();

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        var fuente = plan.Objetos.Single(o => o is { Sistema: SistemaSap.Bw, Tipo: "DataSource", Grupo: GeneradorBodegaBw4Hana.Grupo });
        Assert.Equal("0FI_ACDOCA_10", fuente.NombreTecnico);
        var def = JsonDocument.Parse(fuente.Operacion.Definicion).RootElement;
        Assert.Equal("DATASOURCE_ODP", def.GetProperty("tipo").GetString());
        Assert.Equal("ODP_SAP", def.GetProperty("sistemaFuente").GetString());
    }

    [Fact(DisplayName = "HU-16 y RN-03: con la bodega BW/4HANA existente sus aDSO quedan como Existente")]
    [Trait("HU", "HU-16")]
    public async Task Bw4BodegaExistente()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.ABw4Hana);
        e.Opciones.BodegaExistente = true;
        var c = await e.ClienteEnPlanAsync();

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        Assert.All(plan.Objetos.Where(o => o.Tipo == "aDSO"), o => Assert.Equal(EstadoObjeto.Existente, o.Estado));
    }
}

public class CadenasTests
{
    [Fact(DisplayName = "HU-18 CA1: se crean una cadena full inicial y una delta programada cada 24 horas (RN-04)")]
    [Trait("HU", "HU-18")]
    public async Task FullYDelta()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        var full = JsonDocument.Parse(plan.Objetos.Single(o => o.Tipo == "Cadena de proceso" && o.NombreTecnico == GeneradorCadenas.CadenaFull).Operacion.Definicion).RootElement;
        var delta = plan.Objetos.Single(o => o.Tipo == "Cadena de proceso" && o.NombreTecnico == GeneradorCadenas.CadenaDelta);
        var d = JsonDocument.Parse(delta.Operacion.Definicion).RootElement;
        Assert.Equal("FULL", full.GetProperty("modo").GetString());
        Assert.Equal("UNA_VEZ", full.GetProperty("programacion").GetString());
        Assert.Equal("DELTA", d.GetProperty("modo").GetString());
        Assert.Equal(24, d.GetProperty("programacion").GetProperty("periodoHoras").GetInt32());
        Assert.Contains("RN-04", delta.Nota, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-18 CA2 y CA3: la delta corre a lo sumo cada 24 horas y ninguna cadena borra peticiones solapadas")]
    [Trait("HU", "HU-18")]
    public async Task SinBorradoSolapado()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion, Modulo.ActivosFijos, Modulo.Deterioros], continuar: true);
        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo().Take(3).ToList(), continuar: true);
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501"]);

        var cadenas = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan.Objetos.Where(o => o.Tipo == "Cadena de proceso").ToList();

        Assert.Equal(6, cadenas.Count);
        foreach (var cadena in cadenas)
        {
            var def = JsonDocument.Parse(cadena.Operacion.Definicion).RootElement;
            Assert.False(def.GetProperty("borrarPeticionesSolapadas").GetBoolean());
            if (def.GetProperty("modo").GetString() == "DELTA")
            {
                Assert.True(def.GetProperty("programacion").GetProperty("periodoHoras").GetInt32() <= 24);
            }
        }
    }

    [Fact(DisplayName = "HU-18 CA4: la carga full se ejecuta como parte del paso «Objetos BW y carga full inicial» en UX-09")]
    [Trait("HU", "HU-18")]
    public async Task CargaFullComoPaso()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();

        var ej = await e.GenerarAsync(c.Id);

        var paso = ej.Pasos.Single(p => p.Id == PasosGeneracion.ObjetosBw);
        Assert.Equal("Objetos BW y carga full inicial", paso.Nombre);
        Assert.Equal(EstadoPaso.Completo, paso.Estado);
        Assert.Contains(e.Sap.Llamadas, l => l.Operacion == $"{FuncionesZ.EjecutarCadena} {GeneradorCadenas.CadenaFull}");
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Objetos == $"Ejecución {GeneradorCadenas.CadenaFull}");
        Assert.Equal(GeneradorCadenas.CadenaFull, paso.Objetos[^1].NombreTecnico);
    }
}

public class ModulosBwTests
{
    private static async Task<(Entorno, Cliente)> ConModulosAsync(params Modulo[] modulos)
    {
        var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, modulos, continuar: true);
        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo().Take(3).ToList(), continuar: true);
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501"]);
        return (e, c);
    }

    [Fact(DisplayName = "HU-19 CA1 y HU-20 CA1: activos fijos y deterioros solo se generan si su módulo está marcado")]
    [Trait("HU", "HU-19")]
    [Trait("HU", "HU-20")]
    public async Task SoloConModulo()
    {
        var (e, c) = await ConModulosAsync(Modulo.Consolidacion);
        await using var _ = e;

        var sin = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;
        Assert.DoesNotContain(sin.Pasos, p => p.Id is PasosGeneracion.ActivosFijos or PasosGeneracion.Deterioros);

        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion, Modulo.ActivosFijos, Modulo.Deterioros], continuar: false);
        var con = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;
        Assert.Equal(
            [PasosGeneracion.ObjetosBw, PasosGeneracion.ActivosFijos, PasosGeneracion.Deterioros],
            con.Pasos.Select(p => p.Id));
    }

    [Fact(DisplayName = "HU-19 CA2 y CA3: activos fijos activa su extractor y crea InfoProvider, cargas y cadenas en BW, sin BPC (RN-12)")]
    [Trait("HU", "HU-19")]
    public async Task ActivosFijos()
    {
        var (e, c) = await ConModulosAsync(Modulo.ActivosFijos);
        await using var _ = e;

        var ej = await e.GenerarAsync(c.Id);

        var paso = Assert.Single(ej.Pasos);
        Assert.Equal("Activos fijos en BW", paso.Nombre);
        Assert.Equal(EstadoPaso.Completo, paso.Estado);
        Assert.Contains(paso.Objetos, o => o is { Sistema: SistemaSap.Erp, NombreTecnico: "0FI_AA_11", Estado: EstadoObjeto.Nuevo });
        Assert.Contains(paso.Objetos, o => o is { Tipo: "InfoCubo", NombreTecnico: "ZACC_AF_C01" });
        Assert.Contains(paso.Objetos, o => o is { Tipo: "Cadena de proceso", NombreTecnico: "ZACC_PC_AF_DELTA" });
        Assert.All(paso.Objetos, o => Assert.NotEqual(SistemaSap.Bpc, o.Sistema));
        Assert.DoesNotContain(e.Sap.Objetos, o => o.Sistema == SistemaSap.Bpc);
        Assert.Contains("RN-12", paso.Objetos.Single(o => o.NombreTecnico == "ZACC_AF_C01").Nota, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-20 CA2 y CA3: deterioros activa su extractor y crea InfoProvider, cargas y cadenas en BW, sin BPC (RN-12)")]
    [Trait("HU", "HU-20")]
    public async Task Deterioros()
    {
        var (e, c) = await ConModulosAsync(Modulo.Deterioros);
        await using var _ = e;

        var ej = await e.GenerarAsync(c.Id);

        var paso = Assert.Single(ej.Pasos);
        Assert.Equal("Deterioros en BW", paso.Nombre);
        Assert.Contains(paso.Objetos, o => o is { Sistema: SistemaSap.Erp, NombreTecnico: "0FI_AR_4" });
        Assert.Contains(paso.Objetos, o => o.NombreTecnico == "ZACC_DE_C01");
        Assert.All(paso.Objetos, o => Assert.NotEqual(SistemaSap.Bpc, o.Sistema));
        Assert.Contains(e.Sap.Objetos, o => o.Operacion.NombreTecnico == "ZACC_PC_DE_FULL");
    }

    [Fact(DisplayName = "HU-19 y HU-16: en BW/4HANA los módulos de BW crean aDSO")]
    [Trait("HU", "HU-19")]
    public async Task ModulosEnBw4()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.ABw4Hana);
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.ActivosFijos, Modulo.Deterioros], continuar: true);
        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo().Take(3).ToList(), continuar: true);
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501"]);

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        Assert.Contains(plan.Objetos, o => o is { Tipo: "aDSO", NombreTecnico: "ZACC_AF_D01" });
        Assert.Contains(plan.Objetos, o => o is { Tipo: "aDSO", NombreTecnico: "ZACC_DE_D01" });
        Assert.DoesNotContain(plan.Objetos, o => o.Tipo == "InfoCubo");
    }
}

file static class ObjetoPlanExtensiones
{
    public static string Definicion(this ObjetoPlan o) => o.Operacion.Definicion;
}
