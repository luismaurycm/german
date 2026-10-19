using System.Text.Json;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

public class DatosMaestrosTests
{
    private static JsonElement Def(ObjetoPlan o) => JsonDocument.Parse(o.Operacion.Definicion).RootElement;

    [Fact(DisplayName = "HU-21 CA1: se carga el plan de cuentas de cada sociedad; las que comparten plan lo cargan una vez (RN-05)")]
    [Trait("HU", "HU-21")]
    public async Task PlanesPorSociedad()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();

        var paso = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan.Pasos.Single(p => p.Id == PasosGeneracion.PlanesCuentas);

        Assert.Equal(["Plan PCCO", "Plan PCPA"], paso.Objetos.Select(o => o.NombreTecnico));
        Assert.Equal(["1000", "2000"], Def(paso.Objetos[0]).GetProperty("sociedades").EnumerateArray().Select(x => x.GetString()));
        Assert.All(paso.Objetos, o => Assert.Equal(SistemaSap.Bw, o.Sistema));
    }

    [Fact(DisplayName = "HU-21 CA2, HU-22 CA2 y HU-24 CA3: UX-09 muestra las cantidades cargadas de cada paso")]
    [Trait("HU", "HU-21")]
    [Trait("HU", "HU-22")]
    [Trait("HU", "HU-24")]
    public async Task Cantidades()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();

        var ej = await e.GenerarAsync(c.Id);

        Assert.Equal(EstadoEjecucion.Completa, ej.Estado);
        var pccoCuentas = SimulatedSapGateway.Cuentas.Count(x => x.Plan == "PCCO");
        Assert.Contains($"PCCO: {pccoCuentas} cuentas cargadas.", ej.Pasos.Single(p => p.Id == PasosGeneracion.PlanesCuentas).Detalles);
        Assert.Contains("1000: 12 centros de costo y 5 centros de beneficio cargados.", ej.Pasos.Single(p => p.Id == PasosGeneracion.CentrosCosto).Detalles);
        var nodos = 1 + SimulatedSapGateway.Cuentas.Where(x => x.Plan == "PCCO").Select(x => x.Nodo).Distinct().Count() + pccoCuentas;
        Assert.Contains($"PCCO: {nodos} nodos cargados.", ej.Pasos.Single(p => p.Id == PasosGeneracion.Jerarquia).Detalles);
    }

    [Fact(DisplayName = "HU-21 CA3: una sociedad sin plan de cuentas en el ERP es error del paso con su código")]
    [Trait("HU", "HU-21")]
    public async Task PlanInexistente()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();
        e.Opciones.PlanesSinDatos.Add("PCPA");

        var ej = await e.GenerarAsync(c.Id);

        var paso = ej.PasoConError!;
        Assert.Equal(PasosGeneracion.PlanesCuentas, paso.Id);
        Assert.Equal("3000: el plan de cuentas PCPA no existe en el ERP (SKA1).", paso.Mensaje);
    }

    [Fact(DisplayName = "HU-22 CA1 y CA3: CeCo y CeBe de cada sociedad; un error muestra la sociedad y el mensaje del sistema")]
    [Trait("HU", "HU-22")]
    public async Task CentrosPorSociedad()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();
        var paso = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan.Pasos.Single(p => p.Id == PasosGeneracion.CentrosCosto);
        Assert.Equal(["CeCo y CeBe 1000", "CeCo y CeBe 2000", "CeCo y CeBe 3000"], paso.Objetos.Select(o => o.NombreTecnico));

        e.Opciones.FallaCentrosSociedades.Add("2000");
        var ej = await e.GenerarAsync(c.Id);

        Assert.Equal("2000: el área de controlling asignada a la sociedad no existe (OKKP).", ej.PasoConError!.Mensaje);
        Assert.Single(ej.PasoConError.Detalles);
    }

    [Fact(DisplayName = "HU-23 CA1 y CA2: monedas locales desde T001 y tasas de cierre, promedio e histórica por moneda y periodo (RN-11)")]
    [Trait("HU", "HU-23")]
    public async Task MonedasYTasas()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();

        var paso = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan.Pasos.Single(p => p.Id == PasosGeneracion.Monedas);

        Assert.Equal(["Monedas locales", "Tasas COP/USD"], paso.Objetos.Select(o => o.NombreTecnico));
        Assert.Equal("T001", Def(paso.Objetos[0]).GetProperty("tabla").GetString());
        var tasas = Def(paso.Objetos[1]);
        Assert.Equal(["cierre", "promedio", "historica"], tasas.GetProperty("tipos").EnumerateObject().Select(p => p.Name));
        Assert.Equal(Enumerable.Range(1, 9).Select(m => $"2026-{m:00}"), tasas.GetProperty("periodos").EnumerateArray().Select(x => x.GetString()));

        var ej = await e.GenerarAsync(c.Id);
        Assert.Contains("COP/USD: 27 tasas cargadas (9 periodos × 3 tipos).", ej.Pasos.Single(p => p.Id == PasosGeneracion.Monedas).Detalles);
    }

    [Fact(DisplayName = "HU-23 CA3: una moneda sin tasa para el periodo es error del paso")]
    [Trait("HU", "HU-23")]
    public async Task TasaFaltante()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();
        e.Opciones.TasasFaltantes.Add("COP:2026-06");

        var ej = await e.GenerarAsync(c.Id);

        Assert.Equal(PasosGeneracion.Monedas, ej.PasoConError!.Id);
        Assert.Equal("No hay tasa de cierre COP/USD para 2026-06 en el ERP (TCURR).", ej.PasoConError.Mensaje);
    }

    [Theory(DisplayName = "HU-23: los periodos de tasas van de enero al mes anterior")]
    [Trait("HU", "HU-23")]
    [InlineData(2026, 10, "2026-01", "2026-09", 9)]
    [InlineData(2027, 1, "2026-01", "2026-12", 12)]
    public void PeriodosDeTasas(int anio, int mes, string primero, string ultimo, int total)
    {
        var p = ReglasMonedas.PeriodosCarga(new DateTimeOffset(anio, mes, 15, 0, 0, 0, TimeSpan.Zero));
        Assert.Equal((primero, ultimo, total), (p[0], p[^1], p.Count));
    }

    [Fact(DisplayName = "HU-24 CA1 y CA2: se carga la jerarquía de cada plan; una cuenta fuera del plan se reporta en el paso")]
    [Trait("HU", "HU-24")]
    public async Task Jerarquia()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();
        e.Opciones.CuentasHuerfanasJerarquia.Add("PCCO:8888");

        var ej = await e.GenerarAsync(c.Id);

        var paso = ej.Pasos.Single(p => p.Id == PasosGeneracion.Jerarquia);
        Assert.Equal(EstadoPaso.Completo, paso.Estado);
        Assert.Equal(["Jerarquía PCCO", "Jerarquía PCPA"], paso.Objetos.Select(o => o.NombreTecnico));
        Assert.Equal("PCCO: la jerarquía tiene 8888, que no existe en el plan de cuentas; no se carga.", Assert.Single(paso.Avisos));
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Mensaje.Contains("Aviso: PCCO: la jerarquía tiene 8888", StringComparison.Ordinal));
    }
}

public class ModeloBpcTests
{
    private static JsonElement Def(ObjetoPlan o) => JsonDocument.Parse(o.Operacion.Definicion).RootElement;

    [Fact(DisplayName = "HU-26 CA1: se crean las dimensiones del modelo, entre ellas cuentas, entidades, intercompañía y terceros")]
    [Trait("HU", "HU-26")]
    public async Task Dimensiones()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();

        var paso = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan.Pasos.Single(p => p.Id == PasosGeneracion.ModeloBpc);

        var dims = paso.Objetos.Where(o => o.Tipo == "Dimensión").Select(o => o.NombreTecnico).ToList();
        foreach (var d in new[] { "ACCOUNT", "ENTITY", "INTCO", "TERCERO", "TIME", "RPTCURRENCY", "FLOW", "AUDITTRAIL" })
        {
            Assert.Contains(d, dims);
        }

        Assert.All(paso.Objetos, o => Assert.Equal(SistemaSap.Bpc, o.Sistema));
        Assert.Equal("Respaldo UJBR", paso.Objetos[0].Tipo);
        var modelo = paso.Objetos.Single(o => o.Tipo == "Modelo" && o.NombreTecnico == GeneradorModeloBpc.Modelo);
        Assert.True(paso.Objetos.ToList().IndexOf(modelo) > paso.Objetos.ToList().FindLastIndex(o => o.Tipo == "Miembros" && o.Grupo == GeneradorModeloBpc.Grupo));
        Assert.Contains("RN-02", modelo.Nota, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-26 CA2: los miembros salen de los datos del cliente: cuentas bajo sus rubros, sociedades e intercompañía")]
    [Trait("HU", "HU-26")]
    public async Task Miembros()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();
        var paso = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan.Pasos.Single(p => p.Id == PasosGeneracion.ModeloBpc);
        IReadOnlyList<JsonElement> Miembros(string dim) => Def(paso.Objetos.Single(o => o.Tipo == "Miembros" && o.NombreTecnico == dim)).GetProperty("miembros").EnumerateArray().ToList();

        var cuentas = Miembros("ACCOUNT");
        var caja = cuentas.Single(m => m.GetProperty("id").GetString() == "PCCO_11050501");
        Assert.Equal("F-00-101", caja.GetProperty("padre").GetString());
        Assert.Contains(cuentas, m => m.GetProperty("id").GetString() == "PCPA_1010");
        Assert.Equal(CatalogoRubros.Todos.Count + 4 + ReglasRubros.CuentasTecnicas.Count, cuentas.Count);

        var entidades = Miembros("ENTITY");
        Assert.Equal(["1000", "2000", "3000"], entidades.Select(m => m.GetProperty("id").GetString()));
        Assert.Equal("USD", entidades[2].GetProperty("moneda").GetString());
        Assert.Equal("1000", entidades[1].GetProperty("padre").GetString());
        Assert.Equal(["I_1000", "I_2000", "I_3000", "I_NONE"], Miembros("INTCO").Select(m => m.GetProperty("id").GetString()));
        Assert.Equal(13, Miembros("TIME").Count);
    }

    [Fact(DisplayName = "HU-26 CA3: el modelo se crea en el paso «Modelo BPC, propiedad y scripts» y UX-09 cuenta sus miembros")]
    [Trait("HU", "HU-26")]
    public async Task EjecutaModelo()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();

        var ej = await e.GenerarAsync(c.Id);

        var paso = ej.Pasos.Single(p => p.Id == PasosGeneracion.ModeloBpc);
        Assert.Equal("Modelo BPC, propiedad y scripts", paso.Nombre);
        Assert.Equal(EstadoPaso.Completo, paso.Estado);
        Assert.Contains(e.Sap.Objetos, o => o.Sistema == SistemaSap.Bpc && o.Operacion.NombreTecnico == GeneradorModeloBpc.Modelo);
        Assert.Contains("ENTITY: 3 miembros creados.", paso.Detalles);
        Assert.Contains(e.Sap.Llamadas, l => l.Operacion.StartsWith("Z_ACC_BPC_UJBR", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "HU-26 CA4: un miembro que BPC rechaza se reporta con su código")]
    [Trait("HU", "HU-26")]
    public async Task MiembroRechazado()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.CuentasAdicionales.Add(new CuentaSimulada { Plan = "PCCO", Numero = "12/34", Descripcion = "Cuenta con barra", Nodo = "Disponible", Saldo = 1 });
        var c = await e.ClienteEnPlanAsync();
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["12/34"]);

        var ej = await e.GenerarAsync(c.Id);

        Assert.Equal(PasosGeneracion.ModeloBpc, ej.PasoConError!.Id);
        Assert.StartsWith("UJA_MEMBER: BPC rechazó el miembro «PCCO_12/34» de ACCOUNT", ej.PasoConError.Mensaje, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-26: sin permiso de escritura en BPC el paso falla sin reintento y el reintento no duplica")]
    [Trait("HU", "HU-26")]
    public async Task ErrorYReintento()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();
        e.Opciones.FallaEscritura.Add(SistemaSap.Bpc);

        var ej = await e.GenerarAsync(c.Id);
        Assert.Equal(PasosGeneracion.ModeloBpc, ej.PasoConError!.Id);
        Assert.Equal(1, e.Sap.Llamadas.Count(l => l.EsEscritura && l.Sistema == SistemaSap.Bpc));

        e.Opciones.FallaEscritura.Clear();
        await e.ServicioEjecucion.ReintentarAsync(c.Id);
        await e.ProcesarColaAsync();

        var bpc = e.Sap.Objetos.Where(o => o.Sistema == SistemaSap.Bpc).ToList();
        Assert.Equal(bpc.Count, bpc.Select(o => (o.Operacion.TipoObjeto, o.Operacion.NombreTecnico)).Distinct().Count());
        Assert.Equal(EstadoEjecucion.Completa, (await e.Ejecuciones.ObtenerAsync(ej.Id))!.Estado);
    }

    [Fact(DisplayName = "HU-26 y RN-12: sin el módulo de consolidación no hay modelo de consolidación en BPC")]
    [Trait("HU", "HU-26")]
    public async Task SoloConConsolidacion()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.ActivosFijos], continuar: true);
        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo().Take(3).ToList(), continuar: true);
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501"]);

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;

        Assert.DoesNotContain(plan.Pasos, p => p.Id == PasosGeneracion.ModeloBpc);
        Assert.Empty(plan.DeCapa(Capa.Bpc));
    }
}
