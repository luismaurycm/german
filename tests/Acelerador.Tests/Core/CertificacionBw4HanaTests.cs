using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Core.Reportes;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

/// <summary>HU-58: la variante BW/4HANA con BPC 2021 modelo estándar ejecuta todos los RF de la arquitectura A (RNF-Comp-05).</summary>
public class CertificacionBw4HanaTests
{
    [Fact(DisplayName = "HU-58 CA1 y CA2: con BW/4HANA y BPC 2021 el plan completo (los cuatro módulos) termina, consolida y reporta sin generar InfoCubos")]
    [Trait("HU", "HU-58")]
    public async Task RecorridoCompleto()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.ABw4Hana);
        var c = await e.ClienteListoParaConsolidarAsync(modulos: NotasYPresupuestoTests.Todos);

        var ej = await e.GenerarAsync(c.Id);

        Assert.True(ej.Estado == EstadoEjecucion.Completa, ej.PasoConError?.Nombre + ": " + ej.PasoConError?.Mensaje);
        Assert.Equal(CatalogoVariantes.Bw4HanaBpc2021.Id, ej.VarianteId);
        var objetos = ej.Pasos.SelectMany(p => p.Objetos).ToList();
        Assert.DoesNotContain(objetos, o => o.Tipo == "InfoCubo" || o.Operacion.TipoObjeto == "INFOCUBO");
        Assert.DoesNotContain(e.Sap.Objetos, o => o.Operacion.TipoObjeto == "INFOCUBO");
        Assert.Contains(objetos, o => o.Tipo == "aDSO" && o.NombreTecnico == "ZACC_AF_D01");
        Assert.Contains(objetos, o => o.Tipo == "aDSO" && o.NombreTecnico == "ZACC_PR_D01");
        foreach (var paso in new[] { PasosGeneracion.ObjetosBw, PasosGeneracion.ModeloBpc, PasosGeneracion.Importacion, PasosGeneracion.Presupuesto, PasosGeneracion.Conversion, PasosGeneracion.Consolidacion, PasosGeneracion.Inversion })
        {
            Assert.Contains(ej.Pasos, p => p.Id == paso && p.Terminado);
        }

        var vigentes = ModeloPropiedad.Vigentes(e.Sap.Almacen.Registros, "G_1000", "2026.09").Participaciones;
        var consolidado = MotorConsolidacion.Consolidado(e.Bpc, vigentes, "G_1000", "USD");
        Assert.NotEmpty(consolidado);

        foreach (var r in CatalogoReportes.Todos.Where(r => r.EnUx11))
        {
            var v = (await e.ServicioReportes.ConsultarAsync(c.Id, r.Id, null, null, null, "2026-09", null))!;
            Assert.True(v.Error is null && v.Reporte is not null, $"{r.Id}: {v.Error}");
        }

        var paquete = (await e.ServicioReportes.DescargarAsync(c.Id))!;
        Assert.All(paquete.Generados, g => Assert.True(g.Exitoso, $"{g.Reporte}: {g.Mensaje}"));
        Assert.True((await e.ServicioCierre.ObtenerAsync(c.Id, "2026-09"))!.Filas.All(f => f.Pasos.All(p => p.Estado == EstadoCierre.Completo)));
    }

    [Fact(DisplayName = "HU-58 CA3 (RNF-Comp-04): agregar la variante BW/4HANA no cambia la variante ni el plan de un cliente BW 7.5")]
    [Trait("HU", "HU-58")]
    public async Task ClientesExistentesSinCambios()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.ABw75);
        var c = await e.ClienteListoParaConsolidarAsync();
        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;
        var soloBw75 = new CatalogoVariantes([CatalogoVariantes.Bw75Bpc101]);

        Assert.Equal(CatalogoVariantes.Bw75Bpc101, soloBw75.Buscar(plan.Variante!.Id));
        Assert.Equal(CatalogoVariantes.Bw75Bpc101.Id, plan.Variante.Id);
        Assert.Equal(soloBw75.Buscar(new VersionBw(ProductoBw.Bw75, "7.50"), new VersionBpc(ProductoBpc.Bpc101, "10.1", ModeloBpc.Estandar)),
            CatalogoVariantes.VersionUno.Buscar(new VersionBw(ProductoBw.Bw75, "7.50"), new VersionBpc(ProductoBpc.Bpc101, "10.1", ModeloBpc.Estandar)));
        Assert.Contains(plan.Pasos.SelectMany(p => p.Objetos), o => o.Tipo == "InfoCubo");
    }
}
