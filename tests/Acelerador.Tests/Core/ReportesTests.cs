using System.IO.Compression;
using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Core.Reportes;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Acelerador.Tests.Soporte;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Acelerador.Tests.Core;

/// <summary>HU-42 a HU-45 y HU-48: estructura común de UX-11, rubros por mapeo, reportes R01 a R07 y catálogo de UX-10.</summary>
public class ReportesTests
{
    private static async Task<VistaReporteEpm> R(Entorno e, Cliente c, string id, string? sociedad = null, bool? filiales = null, string? moneda = null, string? periodo = "2026-09", string? rubro = null) =>
        (await e.ServicioReportes.ConsultarAsync(c.Id, id, sociedad, filiales, moneda, periodo, rubro))!;

    private static decimal Valor(ReporteCalculado r, string etiqueta, string columna = MotorReportes.ColumnaTotal) =>
        r.Fila(etiqueta)!.Valores[r.Columnas.ToList().FindIndex(x => x.Id == columna)];

    private static async Task<IReadOnlyDictionary<string, string>> RubroPorCuentaAsync(Entorno e, Guid clienteId) =>
        (await e.Mapeos.ListarAsync(clienteId, "PCCO")).Concat(await e.Mapeos.ListarAsync(clienteId, "PCPA"))
            .ToDictionary(a => GeneradorModeloBpc.IdCuenta(a.PlanCuentas, a.Cuenta), a => a.Rubro)
            .Concat(ReglasRubros.CuentasTecnicas.Select(t => KeyValuePair.Create(t.Id, t.Rubro)))
            .ToDictionary(k => k.Key, k => k.Value);

    [Fact(DisplayName = "HU-42 CA1 y CA2: Incluir filiales cambia entre la sociedad sola y la sociedad con sus filiales, con columna Total (RN-09)")]
    [Trait("HU", "HU-42")]
    public async Task IncluirFiliales()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;

        var con = (await R(e, c, "R01", "1000", true)).Reporte!;
        var sin = (await R(e, c, "R01", "1000", false)).Reporte!;
        var filial = (await R(e, c, "R01", "2000", true)).Reporte!;

        Assert.Equal(["1000", "2000", "3000", MotorReportes.ColumnaEliminaciones, MotorReportes.ColumnaTotal], con.Columnas.Select(x => x.Id));
        Assert.Equal("Total consolidado", con.Columnas[^1].Titulo);
        Assert.Equal(["1000"], sin.Columnas.Select(x => x.Id));
        Assert.Equal(["2000"], filial.Columnas.Select(x => x.Id));
        Assert.Equal(Valor(con, "F-00-101", "1000"), Valor(sin, "F-00-101", "1000"));
        Assert.Equal(new[] { "1000", "2000", "3000", MotorReportes.ColumnaEliminaciones }.Sum(col => Valor(con, "Total activo", col)), Valor(con, "Total activo"));
    }

    [Fact(DisplayName = "HU-42 CA3: la moneda es solo la local del grupo o dólares (RN-06)")]
    [Trait("HU", "HU-42")]
    public async Task SoloMonedaLocalODolares()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;

        var v = await R(e, c, "R01", moneda: "EUR");

        Assert.Equal(["COP", "USD"], v.Contexto.Monedas);
        Assert.Equal("COP", v.Parametros.Moneda);
        var usd = (await R(e, c, "R01", moneda: "USD")).Reporte!;
        Assert.Equal(decimal.Round(Valor(v.Reporte!, "F-00-101", "1000") / 4_000m, 2), decimal.Round(Valor(usd, "F-00-101", "1000"), 2));
    }

    [Fact(DisplayName = "HU-42 CA4: sin BPC, el error de conexión; sin datos, «Sin datos para la sociedad y el periodo elegidos»")]
    [Trait("HU", "HU-42")]
    public async Task SinDatosYSinConexion()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;

        Assert.True((await R(e, c, "R01", periodo: "2026-12")).Reporte!.Vacio);
        Assert.Equal("Sin datos para la sociedad y el periodo elegidos.", MotorReportes.SinDatos);
        e.Opciones.SistemasConFallo.Add(SistemaSap.Bpc);
        var v = await R(e, c, "R01");
        Assert.Null(v.Reporte);
        Assert.NotNull(v.Error);
    }

    [Fact(DisplayName = "HU-43 CA1: cada rubro es la suma de las cuentas mapeadas en UX-06 (RN-10)")]
    [Trait("HU", "HU-43")]
    public async Task RubroEsSumaDeCuentas()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;
        var rubros = await RubroPorCuentaAsync(e, c.Id);

        var r = (await R(e, c, "R01")).Reporte!;

        var esperado = e.Sap.Almacen.Registros.Where(x => x is { Entidad: "1000", Grupo: "", Moneda: "COP", Periodo: "2026.09" } && rubros.GetValueOrDefault(x.Cuenta) == "F-00-102").Sum(x => x.Importe);
        Assert.Equal(esperado, Valor(r, "F-00-102", "1000"));
        Assert.Empty(r.Avisos);
    }

    [Fact(DisplayName = "HU-43 CA2: un cambio en el mapeo se refleja al actualizar el reporte")]
    [Trait("HU", "HU-43")]
    public async Task CambioDeMapeo()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;
        var antes = (await R(e, c, "R01")).Reporte!;
        var bancos = e.Sap.Almacen.Registros.Where(x => x is { Entidad: "1000", Grupo: "", Moneda: "COP", Cuenta: "PCCO_11100501" }).Sum(x => x.Importe);

        await e.Mapeos.GuardarAsync(c.Id, "PCCO", ["11100501"], "F-00-102");
        var despues = (await R(e, c, "R01")).Reporte!;

        Assert.NotEqual(0m, bancos);
        Assert.Equal(Valor(antes, "F-00-101", "1000") - bancos, Valor(despues, "F-00-101", "1000"));
        Assert.Equal(Valor(antes, "F-00-102", "1000") + bancos, Valor(despues, "F-00-102", "1000"));
        Assert.Equal(Valor(antes, "Total activo"), Valor(despues, "Total activo"));
    }

    [Fact(DisplayName = "HU-43 CA3: el detalle de un rubro muestra las cuentas que lo forman")]
    [Trait("HU", "HU-43")]
    public async Task DetalleDelRubro()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;
        var rubros = await RubroPorCuentaAsync(e, c.Id);

        var v = await R(e, c, "R01", rubro: "F-00-101");

        var d = v.Detalle!;
        Assert.Equal(rubros.Where(k => k.Value == "F-00-101").Select(k => k.Key).Order(StringComparer.Ordinal), d.Filas.Where(f => f.Tipo == TipoFila.Detalle).Select(f => f.Etiquetas[0]));
        Assert.Equal(Valor(v.Reporte!, "F-00-101"), d.Filas[^1].Valores[^1]);
        Assert.Equal(d.Filas.Where(f => f.Tipo == TipoFila.Detalle).Sum(f => f.Valores[^1]), d.Filas[^1].Valores[^1]);
    }

    [Fact(DisplayName = "HU-44 CA2: las cifras de R01 cuadran con el balance consolidado del modelo")]
    [Trait("HU", "HU-44")]
    public async Task R01CuadraConElConsolidado()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;
        var rubros = await RubroPorCuentaAsync(e, c.Id);
        var vigentes = ModeloPropiedad.Vigentes(e.Sap.Almacen.Registros, "G_1000", "2026.09").Participaciones;

        foreach (var moneda in new[] { "COP", "USD" })
        {
            var r = (await R(e, c, "R01", moneda: moneda)).Reporte!;
            var consolidado = MotorConsolidacion.Consolidado(e.Sap.Almacen.Registros.Where(x => x.Periodo == "2026.09"), vigentes, "G_1000", moneda);

            Assert.Empty(r.Avisos);
            Assert.Equal(Valor(r, "Total activo"), Valor(r, "Total pasivo y patrimonio"));
            foreach (var fila in r.Filas.Where(f => f.Tipo == TipoFila.Rubro))
            {
                Assert.Equal(MotorReportes.Signo(fila.Rubro!) * consolidado.Where(k => rubros.GetValueOrDefault(k.Key) == fila.Rubro).Sum(k => k.Value), fila.Valores[^1]);
            }

            Assert.Equal(0m, Valor(r, "F-00-107"));
            Assert.NotEqual(0m, Valor(r, "F-00-304"));
        }
    }

    [Fact(DisplayName = "HU-44 CA3: R05 muestra las eliminaciones intercompañía por pareja de sociedades")]
    [Trait("HU", "HU-44")]
    public async Task R05PorPareja()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync(CierreTests.DiferenciaIc);
        await using var _e = e;

        var r = (await R(e, c, "R05")).Reporte!;

        Assert.Equal(["Pareja 1000 ↔ 2000", "Pareja 1000 ↔ 3000", "Eliminación de inversión e interés minoritario"], r.Filas.Where(f => f.Tipo == TipoFila.Seccion).Select(f => f.Etiquetas[2]));
        Assert.Equal(1_000_000_000m, r.Filas.Single(f => f.Etiquetas.SequenceEqual(["1000", "2000", "PCCO_13050501"])).Valores[0]);
        Assert.Equal(-995_000_000m, r.Filas.Single(f => f.Etiquetas.SequenceEqual(["2000", "1000", "PCCO_22050501"])).Valores[0]);
        var diferencias = r.Filas.Where(f => f.Tipo == TipoFila.Subtotal).Select(f => f.Valores[0]).ToList();
        Assert.Equal([5_000_000m, 0m], diferencias);
    }

    [Fact(DisplayName = "HU-44 CA1, CA4 y HU-48 CA3: R01, R05 y R07 se entregan como Excel con EPM conectados al modelo, en el .zip de los módulos elegidos")]
    [Trait("HU", "HU-44")]
    [Trait("HU", "HU-48")]
    public async Task PaqueteZip()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;

        var paquete = (await e.ServicioReportes.DescargarAsync(c.Id))!;

        Assert.EndsWith("_2026-09.zip", paquete.Archivo, StringComparison.Ordinal);
        using var zip = new ZipArchive(new MemoryStream(paquete.Zip));
        Assert.Equal(CatalogoReportes.Todos.Where(r => r.Modulo == Modulo.Consolidacion && r.Disponible).Select(r => r.Archivo).Order(StringComparer.Ordinal),
            zip.Entries.Select(x => x.FullName).Order(StringComparer.Ordinal));
        foreach (var id in new[] { "R01", "R05", "R07" })
        {
            var entrada = zip.Entries.Single(x => x.FullName.StartsWith(id + "_", StringComparison.Ordinal));
            using var ms = new MemoryStream();
            using (var s = entrada.Open())
            {
                s.CopyTo(ms);
            }

            using var doc = SpreadsheetDocument.Open(new MemoryStream(ms.ToArray()), false);
            Assert.Contains(doc.WorkbookPart!.Workbook.DefinedNames!.Elements<DefinedName>(), n => n.Name == "EPM_CONEXION");
            var hoja = (WorksheetPart)doc.WorkbookPart.GetPartById(doc.WorkbookPart.Workbook.Sheets!.Elements<Sheet>().Single().Id!);
            var formulas = hoja.Worksheet.Descendants<CellFormula>().Select(f => f.Text).ToList();
            if (id != "R05")
            {
                Assert.Contains(formulas, f => f.StartsWith($"EPMRetrieveData(\"{GeneradorCierre.Conexion}\"", StringComparison.Ordinal));
            }

            var textos = hoja.Worksheet.Descendants<Cell>().Select(x => x.InnerText).ToList();
            Assert.Contains("Incluir filiales", textos);
        }

        Assert.All(paquete.Generados, g => Assert.True(g.Exitoso, g.Mensaje));
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Paso == ServicioReportes.PasoDescargar);
    }

    [Fact(DisplayName = "HU-45 CA1: R02, R03, R04 y R06 se entregan con la estructura común de UX-11")]
    [Trait("HU", "HU-45")]
    public async Task OlaDos()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;

        var r02 = (await R(e, c, "R02")).Reporte!;
        var r04 = (await R(e, c, "R04")).Reporte!;
        var r06 = (await R(e, c, "R06")).Reporte!;

        Assert.All(new[] { "R02", "R03", "R04", "R06" }, id => Assert.True(CatalogoReportes.Buscar(id)!.EsEstadoFinanciero));
        Assert.Equal(Valor(r02, "Utilidad neta del ejercicio"), Valor(r02, "Propietarios de la controladora") + Valor(r02, "ZACC_RES_IM"));
        Assert.True(Valor(r02, "ZACC_RES_IM") > 0m);
        Assert.Empty(r04.Errores);
        Assert.Empty(r04.Avisos);
        Assert.Equal(Valor((await R(e, c, "R01")).Reporte!, "F-00-101"), r04.Filas[^1].Valores[0]);
        Assert.Contains(r06.Filas, f => f.Etiquetas.Take(4).SequenceEqual(["1000", "2000", "Subsidiaria", ReglasConciliacion.Balance]));
        Assert.Contains(r06.Filas, f => f.Etiquetas.Take(3).SequenceEqual(["2000", "1000", "Matriz"]));
    }

    [Fact(DisplayName = "HU-45 CA2: R03 muestra por separado el interés minoritario y la diferencia en conversión")]
    [Trait("HU", "HU-45")]
    public async Task R03Separado()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;

        var r = (await R(e, c, "R03")).Reporte!;

        Assert.Equal(["ANT", "MOV", "CIE"], r.Columnas.Select(x => x.Id));
        Assert.Equal("Saldo al cierre de 2026-08", r.Columnas[0].Titulo);
        var im = r.Fila(ReglasRubros.CuentaInteresMinoritario.Rubro)!;
        var cta = r.Fila(ReglasRubros.CuentaDiferenciaConversion.Rubro)!;
        Assert.Equal("Interés minoritario", im.Etiquetas[1]);
        Assert.NotEqual(0m, im.Valores[2]);
        Assert.Equal(im.Valores[2], im.Valores[1]);
        Assert.StartsWith("Diferencia en conversión", cta.Etiquetas[1], StringComparison.Ordinal);
        Assert.Equal(Valor((await R(e, c, "R01")).Reporte!, "Total patrimonio"), r.Filas[^1].Valores[2]);
    }

    [Fact(DisplayName = "HU-45 CA3 y HU-48 CA4: R04 clasifica los rubros del flujo; un rubro sin clasificar es un error del reporte con su causa en UX-10")]
    [Trait("HU", "HU-45")]
    [Trait("HU", "HU-48")]
    public async Task R04SinClasificar()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;
        await e.Mapeos.GuardarAsync(c.Id, "PCCO", ["13050501"], "F-00-104");

        var r04 = (await R(e, c, "R04")).Reporte!;
        var generados = await e.ServicioReportes.GenerarAsync(c.Id, ["R01", "R04"]);

        Assert.Equal("Rubros de flujo sin clasificar: F-00-104 Otros activos", Assert.Single(r04.Errores));
        Assert.True(generados.Single(g => g.Reporte == "R01").Exitoso);
        var error = generados.Single(g => g.Reporte == "R04");
        Assert.Equal((false, "Rubros de flujo sin clasificar: F-00-104 Otros activos"), (error.Exitoso, error.Mensaje));
        var fila = (await e.ServicioReportes.CatalogoAsync(c.Id, null))!.Reportes.Single(f => f.Definicion.Id == "R04");
        Assert.Equal(error, fila.Generado);
    }

    [Fact(DisplayName = "HU-48 CA1 y CA2: el catálogo lista ID, reporte, módulo, fuente y ola de los módulos elegidos, con C01 a C04, filtrable por módulo")]
    [Trait("HU", "HU-48")]
    public async Task Catalogo()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion, Modulo.Presupuesto], continuar: true);

        var todos = (await e.ServicioReportes.CatalogoAsync(c.Id, null))!;
        var presupuesto = (await e.ServicioReportes.CatalogoAsync(c.Id, Modulo.Presupuesto))!;

        Assert.Equal(29, todos.Reportes.Count);
        Assert.Equal([Modulo.Consolidacion, Modulo.Presupuesto], todos.Modulos);
        Assert.Equal(["C01", "C02", "C03", "C04"], todos.Reportes.Select(f => f.Definicion.Id).Where(x => x[0] == 'C'));
        Assert.Equal(["P01", "P02", "P03", "P04", "P05"], presupuesto.Reportes.Select(f => f.Definicion.Id));
        Assert.DoesNotContain(todos.Reportes, f => f.Definicion.Fuente == FuenteReporte.Bw);
        Assert.Equal((VistaReporte.Ux13, VistaReporte.Ux15), (CatalogoReportes.Buscar("C02")!.Vista, CatalogoReportes.Buscar("C04")!.Vista));
        Assert.False(todos.Habilitado);
        Assert.Equal(34, CatalogoReportes.Todos.Count);
    }

    [Fact(DisplayName = "HU-41 CA1 y HU-48: al generar los reportes del cierre, la columna Reportes de C04 queda completa")]
    [Trait("HU", "HU-48")]
    [Trait("HU", "HU-41")]
    public async Task ReportesEnC04()
    {
        var (e, c, _) = await CierreTests.ConsolidarAsync();
        await using var _e = e;
        Assert.All((await e.ServicioCierre.ObtenerAsync(c.Id, "2026-09"))!.Filas, f => Assert.Equal(EstadoCierre.Pendiente, f.Pasos[5].Estado));

        await e.ServicioReportes.GenerarAsync(c.Id, CatalogoReportes.DelCierre.Select(r => r.Id).ToList());

        Assert.All((await e.ServicioCierre.ObtenerAsync(c.Id, "2026-09"))!.Filas, f => Assert.Equal(EstadoCierre.Completo, f.Pasos[5].Estado));
    }
}
