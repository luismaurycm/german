using System.Text.Json;
using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Acelerador.Tests.Core;

public class PropiedadTests
{
    internal static async Task<(Entorno, Cliente, EjecucionPlan)> ConsolidarAsync(bool conColigada = false)
    {
        var e = await Entorno.CrearAsync();
        Cliente c;
        if (conColigada)
        {
            c = await e.ClienteEnModulosAsync();
            await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
            await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: true);
            await e.MapearTodoAsync(c.Id);
            await e.ServicioMapeo.ContinuarAsync(c.Id);
            await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "9000.csv", LectorArchivoNoSapTests.Csv(
                "9000;11050501;2026-09;;;;1000000", "9000;31050501;2026-09;;;;-600000", "9000;36050501;2026-09;;;;-400000",
                "9000;41200501;2026-09;1000;;;-500000", "9000;51050501;2026-09;;;;100000"));
            await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000");
            await e.ServicioNoSap.ContinuarAsync(c.Id);
        }
        else
        {
            c = await e.ClienteListoParaConsolidarAsync();
        }

        var ej = await e.GenerarAsync(c.Id);
        Assert.True(ej.Estado == EstadoEjecucion.Completa, ej.PasoConError?.Nombre + ": " + ej.PasoConError?.Mensaje);
        return (e, c, ej);
    }

    [Fact(DisplayName = "HU-32 CA1 y CA3: se crea el modelo de propiedad con la estructura de HU-10 como primer periodo")]
    [Trait("HU", "HU-32")]
    public async Task ModeloConEstructuraInicial()
    {
        var (e, c, ej) = await ConsolidarAsync();
        await using var _ = e;

        var paso = ej.Pasos.Single(p => p.Id == PasosGeneracion.ModeloBpc);
        Assert.Contains(paso.Objetos, o => o is { Tipo: "Modelo", NombreTecnico: ModeloPropiedad.Modelo });
        Assert.Contains(paso.Objetos, o => o is { Tipo: "Plantilla EPM", NombreTecnico: GeneradorPropiedad.Plantilla });
        var (periodo, vigentes) = ModeloPropiedad.Vigentes(e.Sap.Almacen.Registros, "G_1000", "2026.09");
        Assert.Equal("2026.09", periodo);
        Assert.Equal(new Participacion("1000", 100m, 100m, 100m, MetodoConsolidacion.Matriz), vigentes[0]);
        Assert.Equal(new Participacion("2000", 80m, 100m, 80m, MetodoConsolidacion.IntegracionGlobal), vigentes[1]);
        Assert.Equal(GeneradorPropiedad.EstructuraInicial(await e.Sociedades.ListarAsync(c.Id)), vigentes);
    }

    [Fact(DisplayName = "HU-32 CA2: la plantilla C02 se genera conectada al modelo de propiedad y se sube a BPC")]
    [Trait("HU", "HU-32")]
    public async Task PlantillaC02()
    {
        var (e, _, _) = await ConsolidarAsync();
        await using var _e = e;

        var subida = e.Sap.Objetos.Single(o => o.Operacion.TipoObjeto == "PLANTILLA" && o.Operacion.NombreTecnico == GeneradorPropiedad.Plantilla);
        var contenido = Convert.FromBase64String(JsonDocument.Parse(subida.Operacion.Definicion).RootElement.GetProperty("contenido").GetString()!);
        using var doc = SpreadsheetDocument.Open(new MemoryStream(contenido), false);
        var hoja = doc.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Single();
        Assert.Equal("C02", hoja.Name!.Value);
        var ws = ((WorksheetPart)doc.WorkbookPart.GetPartById(hoja.Id!)).Worksheet;
        var textos = ws.Descendants<Cell>().Select(x => x.InnerText).ToList();
        Assert.Contains("C02 Participaciones y métodos", textos);
        Assert.Contains("G_1000", textos);
        var formulas = ws.Descendants<CellFormula>().Select(f => f.Text).ToList();
        Assert.Contains(formulas, f => f.StartsWith("EPMRetrieveData(\"ZACC_PROPIEDAD\",\"POWN\"", StringComparison.Ordinal));
        Assert.Contains("Integración global", ws.Descendants<DataValidation>().Single().Formula1!.Text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-33 CA1, CA3 y CA5: se elimina la inversión solo en integración global, con interés minoritario separado y la diferencia identificable")]
    [Trait("HU", "HU-33")]
    public async Task EliminacionDeInversion()
    {
        var (e, _, ej) = await ConsolidarAsync();
        await using var _e = e;
        decimal Suma(Func<RegistroBpc, bool> f) => e.Bpc.Where(r => r.Origen == "ELIM_INV" && r.Moneda == "COP").Where(f).Sum(r => r.Importe);

        Assert.Equal(["1000", "2000", "3000"], e.Bpc.Where(r => r.Origen == "ELIM_INV").Select(r => r.Entidad).Distinct().Order());
        Assert.Equal(0.2m * (-14_000_000_000m - 4_672_500_000m), Suma(r => r is { Entidad: "2000", Cuenta: "ZACC_IM" }));
        Assert.Equal(0m, Suma(r => r is { Entidad: "3000", Cuenta: "ZACC_IM" }));
        Assert.Equal(11_500_000_000m - 0.8m * 14_000_000_000m, Suma(r => r is { Cuenta: "ZACC_DIF_INV", Intco: "I_2000" }));
        Assert.Equal(-11_500_000_000m, Suma(r => r is { Entidad: "1000", Cuenta: "PCCO_12050501", Intco: "I_2000" }));
        foreach (var moneda in new[] { "COP", "USD" })
        {
            Assert.Equal(0m, e.Bpc.Where(r => r.Origen == "ELIM_INV" && r.Moneda == moneda).Sum(r => r.Importe));
        }

        Assert.Equal("Eliminación de inversión e interés minoritario", ej.Pasos.Single(p => p.Id == PasosGeneracion.Inversion).Nombre);
        Assert.Equal(ClaseCuenta.Patrimonio, ReglasRubros.Clase(ReglasRubros.CuentaInteresMinoritario.Rubro));
    }

    [Fact(DisplayName = "HU-33 CA3: el consolidado cuadra y la inversión en subsidiarias queda en cero")]
    [Trait("HU", "HU-33")]
    public async Task ConsolidadoCuadra()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        var asignaciones = (await e.Mapeos.ListarAsync(c.Id, "PCCO")).Concat(await e.Mapeos.ListarAsync(c.Id, "PCPA"));
        var clases = ReglasRubros.ClasesPorMiembro(asignaciones);
        var vigentes = ModeloPropiedad.Vigentes(e.Sap.Almacen.Registros, "G_1000", "2026.09").Participaciones;

        foreach (var moneda in new[] { "COP", "USD" })
        {
            var consolidado = MotorConsolidacion.Consolidado(e.Bpc, vigentes, "G_1000", moneda);
            Assert.Equal(0m, consolidado.Where(k => ReglasRubros.EsBalance(clases[k.Key])).Sum(k => k.Value));
            Assert.Equal(0m, consolidado.GetValueOrDefault("PCCO_12050501"));
            Assert.NotEqual(0m, consolidado["ZACC_IM"]);
        }
    }

    [Fact(DisplayName = "HU-33 CA2 y HU-35 CA4: el interés minoritario usa los porcentajes del periodo; un cambio no altera periodos anteriores (RN-14)")]
    [Trait("HU", "HU-33")]
    [Trait("HU", "HU-35")]
    public void PorcentajesDelPeriodo()
    {
        var registros = ModeloPropiedad.Registros("G_1000", "2026.09", [new("1000", 100, 100, 100, MetodoConsolidacion.Matriz), new("2000", 80, 100, 80, MetodoConsolidacion.IntegracionGlobal)])
            .Concat(ModeloPropiedad.Registros("G_1000", "2026.10", [new("1000", 100, 100, 100, MetodoConsolidacion.Matriz), new("2000", 90, 100, 90, MetodoConsolidacion.IntegracionGlobal)]))
            .ToList();
        RegistroBpc R(string ent, string cuenta, string intco, decimal v) => new("CONSOLIDACION", ent, cuenta, intco, "2026.09", "COP", "F_CLO", "INPUT", v);
        RegistroBpc[] datos = [R("1000", "INV", "I_2000", 900m), R("1000", "CAJA", "I_NONE", -900m), R("2000", "CAP", "I_NONE", -1_000m), R("2000", "CAJA", "I_NONE", 1_000m)];
        var clases = new Dictionary<string, ClaseCuenta> { ["INV"] = ClaseCuenta.Activo, ["CAJA"] = ClaseCuenta.Activo, ["CAP"] = ClaseCuenta.Patrimonio };
        decimal Im(string periodo) => MotorConsolidacion.EliminarInversion(datos, ModeloPropiedad.Vigentes(registros, "G_1000", periodo).Participaciones, "1000", new HashSet<string> { "INV" }, clases, "G_1000")
            .Where(r => r.Cuenta == "ZACC_IM").Sum(r => r.Importe);

        Assert.Equal(-200m, Im("2026.09"));
        Assert.Equal(-100m, Im("2026.10"));
        Assert.Equal(-100m, Im("2026.11"));
        Assert.Equal(-200m, Im("2026.09"));
        Assert.Equal(80m, ModeloPropiedad.DelPeriodo(registros, "G_1000", "2026.09").Single(p => p.Sociedad == "2000").Propiedad);
        Assert.Empty(ModeloPropiedad.DelPeriodo(registros, "G_1000", "2026.11"));
    }

    [Fact(DisplayName = "HU-34 CA1, CA2 y CA3: la coligada no se integra línea a línea, se reconoce su % del resultado y sus transacciones no se eliminan")]
    [Trait("HU", "HU-34")]
    public async Task MetodoDeParticipacion()
    {
        var (e, _, _) = await ConsolidarAsync(conColigada: true);
        await using var _e = e;
        var vigentes = ModeloPropiedad.Vigentes(e.Sap.Almacen.Registros, "G_1000", "2026.09").Participaciones;

        Assert.Equal(MetodoConsolidacion.Participacion, vigentes.Single(p => p.Sociedad == "9000").Metodo);
        var consolidado = MotorConsolidacion.Consolidado(e.Bpc, vigentes, "G_1000", "COP");
        Assert.Equal(0.35m * -400_000m, consolidado["ZACC_RES_PART"]);
        Assert.Equal(e.Bpc.Where(r => r is { Entidad: "1000" or "2000", Cuenta: "PCCO_11050501", Moneda: "COP", Grupo: "" }).Sum(r => r.Importe), consolidado["PCCO_11050501"]);
        Assert.DoesNotContain(e.Bpc, r => r.Origen == "ELIM_IC" && (r.Entidad == "9000" || r.Intco == "I_9000"));
        Assert.Contains(e.Bpc, r => r is { Entidad: "9000", Intco: "I_1000", Origen: "INPUT" });
        Assert.Equal(0.35m * -400_000m, e.Bpc.Where(r => r.Origen == "PARTICIPACION" && r.Moneda == "COP" && r.Cuenta == "ZACC_RES_PART_EJ").Sum(r => r.Importe));
    }

    [Fact(DisplayName = "HU-34 CA4: una sociedad fuera del perímetro no participa en el periodo")]
    [Trait("HU", "HU-34")]
    public void FueraDelPerimetro()
    {
        RegistroBpc R(string ent, string cuenta, string intco, decimal v) => new("CONSOLIDACION", ent, cuenta, intco, "2026.09", "COP", "F_CLO", "INPUT", v);
        Participacion[] vigentes = [new("1000", 100, 100, 100, MetodoConsolidacion.Matriz), new("3000", 0, 0, 0, MetodoConsolidacion.FueraDelPerimetro)];
        RegistroBpc[] datos = [R("1000", "CAJA", "I_NONE", 10m), R("3000", "CAJA", "I_NONE", 99m), R("1000", "CXC", "I_3000", 5m)];

        Assert.Equal(10m, MotorConsolidacion.Consolidado(datos, vigentes, "G_1000", "COP")["CAJA"]);
        Assert.Empty(MotorConsolidacion.EliminarIntercompania(datos, vigentes.ToDictionary(p => p.Sociedad, p => p.Metodo), "G_1000"));
        Assert.Empty(MotorConsolidacion.EliminarInversion(datos, vigentes, "1000", new HashSet<string>(), new Dictionary<string, ClaseCuenta>(), "G_1000"));
    }
}

public class ParticipacionesC02Tests
{
    private static List<FilaParticipacion> Filas(string propiedad2000 = "80", string control2000 = "80", string? metodo3000 = "IntegracionGlobal") =>
    [
        new("1000", "100", "100", "100", "Matriz"),
        new("2000", control2000, "100", propiedad2000, "IntegracionGlobal"),
        new("3000", "100", "100", "100", metodo3000),
    ];

    [Fact(DisplayName = "HU-35 CA1 y CA4: se guardan los porcentajes y el método de un periodo sin alterar el anterior (RN-14)")]
    [Trait("HU", "HU-35")]
    public async Task GuardarPeriodo()
    {
        var (e, c, _) = await PropiedadTests.ConsolidarAsync();
        await using var _ = e;

        var r = await e.ServicioParticipaciones.GuardarAsync(c.Id, "2026-10", Filas(propiedad2000: "90", control2000: "90"));

        Assert.Equal((3, 0, (string?)null), (r.Guardadas, r.Errores.Count, r.Error));
        var octubre = (await e.ServicioParticipaciones.ObtenerAsync(c.Id, "2026-10"))!;
        Assert.Equal(90m, octubre.Filas.Single(f => f.Sociedad.Codigo == "2000").Actual!.Propiedad);
        Assert.Contains("% propiedad 80 → 90", octubre.Filas.Single(f => f.Sociedad.Codigo == "2000").Cambios);
        var septiembre = (await e.ServicioParticipaciones.ObtenerAsync(c.Id, "2026-09"))!;
        Assert.Equal(80m, septiembre.Filas.Single(f => f.Sociedad.Codigo == "2000").Actual!.Propiedad);
    }

    [Fact(DisplayName = "HU-35 CA2: Copiar del periodo anterior trae las participaciones del mes previo")]
    [Trait("HU", "HU-35")]
    public async Task CopiarAnterior()
    {
        var (e, c, _) = await PropiedadTests.ConsolidarAsync();
        await using var _ = e;
        await e.ServicioParticipaciones.GuardarAsync(c.Id, "2026-10", Filas(propiedad2000: "90"));

        var copia = await e.ServicioParticipaciones.CopiarAnteriorAsync(c.Id, "2026-11");

        Assert.Equal(90m, copia.Single(p => p.Sociedad == "2000").Propiedad);
        Assert.True((await e.ServicioParticipaciones.ObtenerAsync(c.Id, "2026-11"))!.Vacio);
    }

    [Theory(DisplayName = "HU-35 CA3: un porcentaje fuera de 0 a 100 o sin método marca la celda y no guarda la fila")]
    [Trait("HU", "HU-35")]
    [InlineData("150", "IntegracionGlobal", "control")]
    [InlineData("−0,01", "IntegracionGlobal", "control")]
    [InlineData("100,01", "IntegracionGlobal", "control")]
    [InlineData("80", null, "metodo")]
    public async Task ValidacionPorCelda(string control2000, string? metodo3000, string dato)
    {
        var (e, c, _) = await PropiedadTests.ConsolidarAsync();
        await using var _ = e;

        var r = await e.ServicioParticipaciones.GuardarAsync(c.Id, "2026-10", Filas(control2000: control2000, metodo3000: metodo3000));

        var error = Assert.Single(r.Errores);
        Assert.Equal(dato, error.Dato);
        Assert.Equal(2, r.Guardadas);
        var octubre = (await e.ServicioParticipaciones.ObtenerAsync(c.Id, "2026-10"))!;
        Assert.Null(octubre.Filas.Single(f => f.Sociedad.Codigo == error.Sociedad).Actual);
        Assert.StartsWith($"No se guardó la fila {error.Sociedad}.", ReglasParticipaciones.MensajeFila(error.Sociedad, r.Errores), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-35 CA5: cada cambio queda registrado con usuario, fecha, valor anterior y nuevo (RNF-Aud-02)")]
    [Trait("HU", "HU-35")]
    public async Task Auditoria()
    {
        var (e, c, _) = await PropiedadTests.ConsolidarAsync();
        await using var _ = e;
        e.Reloj.Ahora = e.Reloj.Ahora.AddDays(3);

        await e.ServicioParticipaciones.GuardarAsync(c.Id, "2026-09", Filas(propiedad2000: "85"));

        var cambio = Assert.Single((await e.ServicioParticipaciones.ObtenerAsync(c.Id, "2026-09"))!.Auditoria, a => a.Usuario == @"CM\consultor1");
        Assert.Equal(("2000", "2026.09", "POWN", "80", "85", e.Reloj.Ahora), (cambio.Sociedad, cambio.Periodo, cambio.Dato, cambio.Anterior, cambio.Nuevo, cambio.Fecha));
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Paso == ServicioParticipaciones.PasoGuardar && x.Resultado == ResultadoEjecucion.Exitoso);
    }

    [Fact(DisplayName = "HU-35: un periodo sin participaciones muestra el estado vacío")]
    [Trait("HU", "HU-35")]
    public async Task Vacio()
    {
        var (e, c, _) = await PropiedadTests.ConsolidarAsync();
        await using var _ = e;

        var v = (await e.ServicioParticipaciones.ObtenerAsync(c.Id, "2026-12"))!;

        Assert.True(v.Vacio);
        Assert.Equal(12, v.Periodos.Count);
        Assert.Equal("Sin participaciones para este periodo: copia las del periodo anterior o ingrésalas.", ServicioParticipaciones.MensajeVacio);
    }
}
