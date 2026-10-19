using System.Text.Json;
using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-36")]
public class ReglaCuadreTests
{
    private static readonly Dictionary<string, string> Mapeo = new()
    {
        ["1105"] = "F-00-101",
        ["2205"] = "F-00-201",
        ["3105"] = "F-00-301",
        ["3605"] = "F-00-302",
        ["4120"] = "F-00-401",
        ["5105"] = "F-00-501",
    };

    private static IEnumerable<SaldoErp> Saldos(decimal ajusteBalance = 0m, decimal ajusteResultado = 0m) =>
    [
        new("1105", "", 1_000m + ajusteBalance),
        new("2205", "", -300m),
        new("3105", "", -500m),
        new("3605", "", -200m + ajusteResultado),
        new("4120", "", -700m),
        new("5105", "", 500m),
    ];

    [Fact(DisplayName = "HU-36 CA1 y CA2: una sociedad que cuadra (balance y resultado) se puede importar")]
    public void Cuadra()
    {
        var v = ReglaCuadre.Validar("1000", "2026-09", Saldos(), Mapeo, 1m);
        Assert.True(v.Importable);
        Assert.Equal((0m, 0m), (v.DiferenciaBalance, v.DiferenciaResultado));
    }

    [Theory(DisplayName = "HU-36 CA1: activo = pasivo + patrimonio dentro de la tolerancia; la diferencia igual a la tolerancia pasa (RN-16)")]
    [InlineData("0.99", true)]
    [InlineData("1.00", true)]
    [InlineData("1.01", false)]
    [InlineData("-1.01", false)]
    [InlineData("5000000", false)]
    public void ToleranciaDeBalance(string ajuste, bool importable)
    {
        var d = decimal.Parse(ajuste, System.Globalization.CultureInfo.InvariantCulture);

        var v = ReglaCuadre.Validar("1000", "2026-09", Saldos(ajusteBalance: d), Mapeo, ReglaCuadre.ToleranciaProvisional);

        Assert.Equal(importable, v.Importable);
        Assert.Equal(d, v.DiferenciaBalance);
    }

    [Fact(DisplayName = "HU-36 CA2: la utilidad del estado de resultados debe ser igual al resultado del ejercicio del balance")]
    public void UtilidadIgualAlResultado()
    {
        var v = ReglaCuadre.Validar("3000", "2026-09", [.. Saldos(), new("1105", "", 50m), new("3605", "", -50m)], Mapeo, 1m);

        Assert.False(v.Importable);
        Assert.Equal(0m, v.DiferenciaBalance);
        Assert.Equal(50m, v.DiferenciaResultado);
        Assert.Equal("3000 no se importa (2026-09): utilidad − resultado del ejercicio = 50,00.", ReglaCuadre.Motivo(v, 1m));
    }

    [Fact(DisplayName = "HU-36 CA3: todas las cuentas con saldo deben estar mapeadas")]
    public void CuentasMapeadas()
    {
        var v = ReglaCuadre.Validar("1000", "2026-09", [.. Saldos(), new("9999", "", 10m), new("9998", "", 0m)], Mapeo, 1m);

        Assert.False(v.Importable);
        Assert.Equal(["9999"], v.CuentasSinMapear);
        Assert.Contains("cuentas con saldo sin mapear: 9999", ReglaCuadre.Motivo(v, 1m), StringComparison.Ordinal);
    }
}

public class ConsolidacionTests
{
    private static async Task<(Entorno, Cliente, EjecucionPlan)> ConsolidarAsync(Action<OpcionesSimulador>? antes = null, List<FilaSociedad>? filas = null)
    {
        var e = await Entorno.CrearAsync();
        var c = await e.ClienteListoParaConsolidarAsync(filas);
        antes?.Invoke(e.Opciones);
        var ej = await e.GenerarAsync(c.Id);
        return (e, c, ej);
    }

    private static decimal Suma(IEnumerable<RegistroBpc> r) => r.Sum(x => x.Importe);

    [Fact(DisplayName = "HU-36 CA1, CA4 y CA5: una sociedad descuadrada no se importa, queda en C03 con la diferencia y el paso la lista")]
    [Trait("HU", "HU-36")]
    [Trait("HU", "HU-27")]
    public async Task SociedadDescuadrada()
    {
        var (e, c, ej) = await ConsolidarAsync(o => o.AjustesSaldo.Add("2000:11050501:5000000"));
        await using var _ = e;

        var cuadre = ej.Pasos.Single(p => p.Id == PasosGeneracion.Cuadre);
        Assert.Equal("Validación de cuadre", cuadre.Nombre);
        Assert.Equal(EstadoPaso.Completo, cuadre.Estado);
        Assert.Equal("2000 no se importa (2026-09): activo − (pasivo + patrimonio) = 5.000.000,00.", Assert.Single(cuadre.Avisos));
        Assert.DoesNotContain(e.Bpc, r => r.Entidad == "2000");
        Assert.Contains(e.Bpc, r => r.Entidad == "1000");
        var v2000 = e.Sap.Almacen.Validaciones.Single(v => v.Sociedad == "2000");
        Assert.Equal((false, false, 5_000_000m), (v2000.Importable, v2000.Importada, v2000.DiferenciaBalance));
        Assert.True(e.Sap.Almacen.Validaciones.Single(v => v.Sociedad == "1000").Importada);
        Assert.Contains("2000 no se importa en 2026-09: no pasó la validación de cuadre (ver C03).", ej.Pasos.Single(p => p.Id == PasosGeneracion.Importacion).Avisos);
    }

    [Fact(DisplayName = "HU-36 CA3: con cuentas con saldo sin mapear ninguna sociedad llega a BPC")]
    [Trait("HU", "HU-36")]
    public async Task SinMapeoCompleto()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();

        var ej = await e.GenerarAsync(c.Id);

        Assert.Equal(3, ej.Pasos.Single(p => p.Id == PasosGeneracion.Cuadre).Avisos.Count);
        Assert.All(e.Sap.Almacen.Validaciones, v => Assert.NotEmpty(v.CuentasSinMapear));
        Assert.Empty(e.Bpc);
        Assert.Equal(0, ej.SociedadesConsolidadas);
    }

    [Fact(DisplayName = "HU-27 CA2 y CA4: un paquete de Data Manager se ejecuta por sociedad y periodo y UX-09 muestra los registros")]
    [Trait("HU", "HU-27")]
    public async Task ImportacionPorSociedad()
    {
        var (e, _, ej) = await ConsolidarAsync();
        await using var _e = e;

        var paso = ej.Pasos.Single(p => p.Id == PasosGeneracion.Importacion);
        Assert.Equal("Importación a BPC", paso.Nombre);
        Assert.Equal(["ZACC_IMPORTAR", "Importar 1000 2026-09", "Importar 2000 2026-09", "Importar 3000 2026-09"], paso.Objetos.Select(o => o.NombreTecnico));
        var en1000 = e.Bpc.Count(r => r is { Entidad: "1000", Moneda: "LC" });
        Assert.Contains($"1000: {en1000} registros importados en 2026-09.", paso.Detalles);
        Assert.All(e.Bpc.Where(r => r.Moneda == "LC"), r => Assert.Equal(("2026.09", "INPUT", "F_CLO"), (r.Periodo, r.Origen, r.Flujo)));
        Assert.Equal(3, ej.SociedadesConsolidadas);
    }

    [Fact(DisplayName = "HU-27: repetir la importación de una sociedad y periodo reemplaza sus datos sin duplicar")]
    [Trait("HU", "HU-27")]
    public async Task ImportarDosVecesNoDuplica()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        var antes = e.Bpc.Count;
        var total = Suma(e.Bpc.Where(r => r is { Entidad: "1000", Moneda: "LC" }));

        await e.GenerarAsync(c.Id);

        Assert.Equal(antes, e.Bpc.Count);
        Assert.Equal(total, Suma(e.Bpc.Where(r => r is { Entidad: "1000", Moneda: "LC" })));
    }

    [Fact(DisplayName = "HU-27 CA1: la importación solo se genera en arquitectura A con BPC estándar (RN-19)")]
    [Trait("HU", "HU-27")]
    public async Task SoloArquitecturaA()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteListoParaConsolidarAsync();
        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;
        Assert.Contains(plan.Pasos, p => p.Id == PasosGeneracion.Importacion);

        var b = c.Deteccion! with { Arquitectura = Arquitectura.B };
        c.Deteccion = b;
        var contexto = new ContextoPlan(c, CatalogoVariantes.Bw75Bpc101, (await e.Inventarios.ObtenerAsync(c.Id))!, await e.Sociedades.ListarAsync(c.Id), [], e.Reloj.Ahora);
        Assert.DoesNotContain(new GeneradorConsolidacion().Generar(contexto), o => o.PasoId == PasosGeneracion.Importacion);
    }

    [Fact(DisplayName = "HU-28 CA1 y CA2: los scripts usan la variante de modelo estándar y traen los códigos del cliente sin edición manual")]
    [Trait("HU", "HU-28")]
    public async Task Scripts()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteListoParaConsolidarAsync();

        var paso = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan.Pasos.Single(p => p.Id == PasosGeneracion.ModeloBpc);

        var eliminacion = JsonDocument.Parse(paso.Objetos.Single(o => o.NombreTecnico == GeneradorScripts.ScriptEliminacion).Operacion.Definicion).RootElement.GetProperty("contenido").GetString()!;
        Assert.Contains("Variante para modelo estándar: A-BW75-BPC101-STD", eliminacion, StringComparison.Ordinal);
        Assert.Contains("OTHER = [ENTITY=1000,2000,3000]", eliminacion, StringComparison.Ordinal);
        Assert.Contains("GROUP = G_1000", eliminacion, StringComparison.Ordinal);
        Assert.Contains("PCCO_13050501", eliminacion, StringComparison.Ordinal);
        Assert.Contains("ZACC_DIF_IC", eliminacion, StringComparison.Ordinal);
        Assert.DoesNotContain("%SOCIEDADES%", eliminacion, StringComparison.Ordinal);
        var conversion = JsonDocument.Parse(paso.Objetos.Single(o => o.NombreTecnico == GeneradorScripts.ScriptConversion).Operacion.Definicion).RootElement.GetProperty("contenido").GetString()!;
        Assert.Contains("CURRENCY = COP,USD", conversion, StringComparison.Ordinal);
        Assert.All(paso.Objetos.Where(o => o.Tipo == "Script"), o => Assert.Contains("RN-02", o.Nota, StringComparison.Ordinal));
        var embebida = CatalogoVariantes.Bw75Bpc101 with { Id = "A-BW75-BPC101-EMB", Modelo = ModeloBpc.Embebido };
        Assert.False(new GeneradorScripts().Aplica(new ContextoPlan(c, embebida, (await e.Inventarios.ObtenerAsync(c.Id))!, [], [], e.Reloj.Ahora)));
    }

    [Fact(DisplayName = "HU-28 CA4: antes de reemplazar un script existente se respalda el ambiente con UJBR")]
    [Trait("HU", "HU-28")]
    public async Task RespaldoAntesDeReemplazar()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        var antes = e.Sap.Llamadas.Count;

        var ej = await e.GenerarAsync(c.Id);

        var segunda = e.Sap.Llamadas.Skip(antes).Select(l => l.Operacion).ToList();
        var ujbr = segunda.FindIndex(l => l.StartsWith("Z_ACC_BPC_UJBR", StringComparison.Ordinal));
        var script = segunda.FindIndex(l => l.StartsWith(FuncionesBpc.DesplegarScript, StringComparison.Ordinal));
        Assert.True(ujbr >= 0 && ujbr < script, "El respaldo UJBR va antes de desplegar los scripts.");
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Mensaje == $"{GeneradorScripts.ScriptEliminacion} reemplazado en UJFS.");
        Assert.Equal(EstadoEjecucion.Completa, ej.Estado);
    }

    [Fact(DisplayName = "HU-30 CA1 y CA2: balance a tasa de cierre, resultados a promedio y patrimonio a histórica; quedan las dos monedas (RN-06, RN-11)")]
    [Trait("HU", "HU-30")]
    public async Task Conversion()
    {
        var (e, _, ej) = await ConsolidarAsync();
        await using var _e = e;
        RegistroBpc Uno(string entidad, string cuenta, string moneda) => e.Bpc.Single(r => r.Entidad == entidad && r.Cuenta == cuenta && r.Moneda == moneda && r.Intco == "I_NONE" && r.Origen == "INPUT");

        Assert.Equal(EstadoEjecucion.Completa, ej.Estado);
        Assert.Equal(152_300_000m / 4_000m, Uno("1000", "PCCO_11050501", "USD").Importe);
        Assert.Equal(decimal.Round(-36_140_000_000m / 3_950m, 2), Uno("1000", "PCCO_41200501", "USD").Importe);
        Assert.Equal(decimal.Round(-40_000_000_000m / 3_800m, 2), Uno("1000", "PCCO_31050501", "USD").Importe);
        Assert.Equal(152_300_000m, Uno("1000", "PCCO_11050501", "COP").Importe);
        Assert.Equal(85_000m * 4_000m, Uno("3000", "PCPA_1010", "COP").Importe);
        Assert.Equal(85_000m, Uno("3000", "PCPA_1010", "USD").Importe);
        foreach (var s in new[] { "1000", "2000", "3000" })
        {
            Assert.Contains(e.Bpc, r => r.Entidad == s && r.Moneda == "COP");
            Assert.Contains(e.Bpc, r => r.Entidad == s && r.Moneda == "USD");
        }
    }

    [Fact(DisplayName = "HU-31 CA1 y CA2: la diferencia en conversión va al patrimonio y el balance convertido suma cero (RN-18)")]
    [Trait("HU", "HU-31")]
    public async Task DiferenciaEnConversion()
    {
        var (e, c, ej) = await ConsolidarAsync();
        await using var _e = e;
        var clases = ReglasRubros.ClasesPorMiembro(await e.Mapeos.ListarAsync(c.Id, "PCCO") is var a1 ? [.. a1, .. await e.Mapeos.ListarAsync(c.Id, "PCPA")] : []);

        foreach (var s in new[] { "1000", "2000", "3000" })
        {
            foreach (var m in new[] { "COP", "USD" })
            {
                var registros = e.Bpc.Where(r => r.Entidad == s && r.Moneda == m && r.Grupo.Length == 0).ToList();
                Assert.Equal(0m, registros.Where(r => ReglasRubros.EsBalance(clases[r.Cuenta])).Sum(r => r.Importe));
                Assert.All(registros.Where(r => r.Cuenta == "ZACC_CTA"), r => Assert.Equal(("F_CTA", "CONVERSION"), (r.Flujo, r.Origen)));
            }
        }

        Assert.Contains(e.Bpc, r => r is { Entidad: "1000", Moneda: "USD", Cuenta: "ZACC_CTA" } && r.Importe != 0m);
        Assert.DoesNotContain(e.Bpc, r => r is { Entidad: "1000", Moneda: "COP", Cuenta: "ZACC_CTA" });
        Assert.Equal(ClaseCuenta.Patrimonio, clases["ZACC_CTA"]);
        Assert.Contains(ej.Pasos.Single(p => p.Id == PasosGeneracion.Conversion).Detalles, d => d.Contains("diferencia en conversión en ZACC_CTA", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "HU-30 CA4: si falta una tasa del periodo la conversión falla e indica la moneda")]
    [Trait("HU", "HU-30")]
    public void FaltaTasa()
    {
        RegistroBpc[] locales = [new("CONSOLIDACION", "1000", "PCCO_11050501", "I_NONE", "2026.09", "LC", "F_CLO", "INPUT", 100m)];

        var (registros, falta) = MotorConsolidacion.Convertir(locales, "COP", "USD", new Dictionary<string, ClaseCuenta>(), (m, t) => m == "USD" ? 1m : null);

        Assert.Empty(registros);
        Assert.Equal("COP (cierre)", falta);
    }

    [Fact(DisplayName = "HU-30 CA4: el paso de conversión falla con la moneda cuando BPC no tiene la tasa")]
    [Trait("HU", "HU-30")]
    public void FaltaTasaEnBpc()
    {
        var sim = new SimulatedSapGateway(new OpcionesSimulador { TasasFaltantes = ["COP:2026-09"] }, new LogicaBpcReferencia());
        sim.Almacen.Reemplazar(_ => false, [new("CONSOLIDACION", "1000", "PCCO_11050501", "I_NONE", "2026.09", "LC", "F_CLO", "INPUT", 100m)]);
        var def = Definicion.Json(new { logica = "CONVERSION", periodo = "2026-09", monedas = new Dictionary<string, string> { ["1000"] = "COP" }, presentacion = new[] { "COP", "USD" }, clases = new Dictionary<string, string>() });

        var r = new LogicaBpcReferencia().Ejecutar(new OperacionSap(FuncionesBpc.EjecutarLogica, "LOGICA", "Conversión 2026-09", def), sim.Almacen, new OpcionesSimulador { TasasFaltantes = ["COP:2026-09"] })!;

        Assert.False(r.Exitosa);
        Assert.Equal("CURR_CONVERSION: falta la tasa COP (cierre) para 2026-09; no se convierte 1000.", r.Mensaje);
    }

    [Fact(DisplayName = "HU-29 CA1 y CA2: solo participan las sociedades del grupo y se eliminan sus transacciones intercompañía")]
    [Trait("HU", "HU-29")]
    public async Task EliminacionIntercompania()
    {
        var (e, _, ej) = await ConsolidarAsync();
        await using var _e = e;

        Assert.Equal(["1000", "2000", "3000"], e.Bpc.Select(r => r.Entidad).Distinct().Order());
        var elim = e.Bpc.Where(r => r.Origen == "ELIM_IC").ToList();
        Assert.NotEmpty(elim);
        Assert.All(elim, r => Assert.Equal("G_1000", r.Grupo));
        foreach (var moneda in new[] { "COP", "USD" })
        {
            // Cada partida intercompañía entre sociedades del grupo queda en cero en el consolidado.
            var ic = e.Bpc.Where(r => r.Moneda == moneda && r.Intco != "I_NONE" && r.Cuenta != "ZACC_DIF_IC");
            Assert.Equal(0m, ic.Sum(r => r.Importe));
            Assert.Equal(0m, e.Bpc.Where(r => r.Moneda == moneda && r.Cuenta == "ZACC_DIF_IC").Sum(r => r.Importe));
        }

        var paso = ej.Pasos.Single(p => p.Id == PasosGeneracion.Consolidacion);
        Assert.Equal("Consolidación", paso.Nombre);
        Assert.Contains(paso.Detalles, d => d.StartsWith("Eliminaciones intercompañía de G_1000:", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "HU-29 CA2 y C01: si las partes no concilian, la diferencia queda en la cuenta de diferencias intercompañía")]
    [Trait("HU", "HU-29")]
    public void DiferenciaNoConciliada()
    {
        RegistroBpc R(string ent, string cuenta, string intco, decimal v) => new("CONSOLIDACION", ent, cuenta, intco, "2026.09", "COP", "F_CLO", "INPUT", v);
        var metodos = new Dictionary<string, MetodoConsolidacion> { ["1000"] = MetodoConsolidacion.Matriz, ["2000"] = MetodoConsolidacion.IntegracionGlobal, ["9000"] = MetodoConsolidacion.Participacion };

        var elim = MotorConsolidacion.EliminarIntercompania([R("1000", "CxC", "I_2000", 1_000m), R("2000", "CxP", "I_1000", -990m), R("1000", "CxC", "I_9000", 50m)], metodos, "G_1000");

        Assert.Equal(10m, elim.Where(r => r.Cuenta == "ZACC_DIF_IC").Sum(r => r.Importe));
        Assert.DoesNotContain(elim, r => r.Intco == "I_9000");
    }

    [Fact(DisplayName = "HU-29 CA3: si la eliminación falla el paso «Consolidación» muestra la causa y se puede reintentar")]
    [Trait("HU", "HU-29")]
    public async Task ConsolidacionFalla()
    {
        var (e, c, ej) = await ConsolidarAsync(o => o.FallaLogica.Add("Eliminación intercompañía G_1000 2026-09"));
        await using var _e = e;

        Assert.Equal(PasosGeneracion.Consolidacion, ej.PasoConError!.Id);
        Assert.StartsWith("UJK: la lógica Eliminación intercompañía G_1000 2026-09 terminó con error", ej.PasoConError.Mensaje, StringComparison.Ordinal);

        e.Opciones.FallaLogica.Clear();
        await e.ServicioEjecucion.ReintentarAsync(c.Id);
        await e.ProcesarColaAsync();
        Assert.Equal(EstadoEjecucion.Completa, (await e.Ejecuciones.ObtenerAsync(ej.Id))!.Estado);
    }

    [Fact(DisplayName = "HU-29 CA4: el resultado de la eliminación queda consultable en BPC para R05")]
    [Trait("HU", "HU-29")]
    public async Task ConsultableParaR05()
    {
        var (e, _, _) = await ConsolidarAsync();
        await using var _e = e;
        var bpc = new DestinoSap(SistemaSap.Bpc, new ParametrosConexion("bw-ejemplo.local", "100", "RFC_ACEL"), new CredencialSap("x"));

        var r = await e.Sap.ConsultarBpcAsync(bpc, new ConsultaBpc(GeneradorModeloBpc.Modelo, "2026.09", "COP", "ELIM_IC"));

        Assert.True(r.Exitosa);
        Assert.NotEmpty(r.Registros);
        Assert.All(r.Registros, x => Assert.Equal("ELIM_IC", x.Origen));
        Assert.Equal(3, r.Validaciones.Count);
    }

    [Fact(DisplayName = "HU-36 CA1 y HU-25 CA5: la carga no SAP pasa por la validación de cuadre antes de llegar a BPC")]
    [Trait("HU", "HU-36")]
    [Trait("HU", "HU-25")]
    public async Task NoSapValidada()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: true);
        await e.MapearTodoAsync(c.Id);
        await e.ServicioMapeo.ContinuarAsync(c.Id);
        await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "9000.csv", LectorArchivoNoSapTests.Csv(
            "9000;11050501;2026-09;;;;1000000", "9000;31050501;2026-09;;;;-600000", "9000;36050501;2026-09;;;;-400000",
            "9000;41200501;2026-09;;;;-500000", "9000;51050501;2026-09;;;;100000"));
        await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000");
        await e.ServicioNoSap.ContinuarAsync(c.Id);

        var ej = await e.GenerarAsync(c.Id);

        Assert.True(ej.Estado == EstadoEjecucion.Completa, ej.PasoConError?.Nombre + ": " + ej.PasoConError?.Mensaje);
        Assert.True(e.Sap.Almacen.Validaciones.Single(v => v.Sociedad == "9000").Importada);
        Assert.Equal(1_000_000m, e.Bpc.Single(r => r is { Entidad: "9000", Cuenta: "PCCO_11050501", Moneda: "LC" }).Importe);
        Assert.DoesNotContain(e.Bpc, r => r.Origen == "ELIM_IC" && (r.Entidad == "9000" || r.Intco == "I_9000"));
    }
}
