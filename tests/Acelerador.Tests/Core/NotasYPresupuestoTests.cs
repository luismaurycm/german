using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reportes;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

/// <summary>HU-46, HU-47 y HU-49 a HU-53: notas R08 a R20, modelo de planeación, P05, P01 a P04 y reportes de BW.</summary>
public class NotasYPresupuestoTests
{
    internal static readonly Modulo[] Todos = [Modulo.Consolidacion, Modulo.Presupuesto, Modulo.ActivosFijos, Modulo.Deterioros];

    internal static async Task<(Entorno, Cliente, EjecucionPlan)> ConsolidarAsync(IReadOnlyList<Modulo>? modulos = null)
    {
        var e = await Entorno.CrearAsync();
        var c = await e.ClienteListoParaConsolidarAsync(modulos: modulos ?? Todos);
        var ej = await e.GenerarAsync(c.Id);
        Assert.True(ej.Estado == EstadoEjecucion.Completa, ej.PasoConError?.Nombre + ": " + ej.PasoConError?.Mensaje);
        return (e, c, ej);
    }

    private static async Task<VistaReporteEpm> R(Entorno e, Cliente c, string id, string? sociedad = null, bool? filiales = null, string? moneda = null, string? version = null, int? anio = null) =>
        (await e.ServicioReportes.ConsultarAsync(c.Id, id, sociedad, filiales, moneda, "2026-09", null, version, anio))!;

    private static decimal Total(ReporteCalculado r, string etiqueta, int columna = -1)
    {
        var fila = r.Fila(etiqueta)!;
        return columna < 0 ? fila.Valores[^1] : fila.Valores[columna];
    }

    [Fact(DisplayName = "HU-46 CA1 y CA3: R08 a R14 tienen la estructura común y cada nota cuadra con su rubro de R01")]
    [Trait("HU", "HU-46")]
    public async Task NotasCuadranConR01()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        var r01 = (await R(e, c, "R01")).Reporte!;

        foreach (var id in new[] { "R08", "R09", "R10", "R11", "R12", "R13", "R14" })
        {
            var nota = (await R(e, c, id)).Reporte!;
            var rubro = MotorReportes.Notas[id][0];
            Assert.Equal(["Cuenta", "Descripción"], nota.Encabezados);
            Assert.Equal(r01.Columnas, nota.Columnas);
            Assert.Equal(r01.Fila(rubro)?.Valores[^1] ?? 0m, Total(nota, rubro));
        }

        var r10 = (await R(e, c, "R10")).Reporte!;
        Assert.Equal(["PCCO_15040501", "PCCO_15160501", "PCCO_15240501", "PCCO_15920501", "PCPA_1500"], r10.Filas.Where(f => f.Tipo == TipoFila.Detalle).Select(f => f.Etiquetas[0]));
        Assert.True((await R(e, c, "R09")).Reporte!.Vacio);
    }

    [Fact(DisplayName = "HU-46 CA2: R08, R10 y R11 incluyen los movimientos de activos fijos y deterioros que vienen de BW (RF-30)")]
    [Trait("HU", "HU-46")]
    public async Task MovimientosDeBw()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        var r10 = await R(e, c, "R10", sociedad: "1000", filiales: false);
        var r08 = await R(e, c, "R08", sociedad: "1000", filiales: false);
        var r11 = await R(e, c, "R11", sociedad: "1000", filiales: false);

        Assert.Equal(["Construcciones y edificaciones", "Equipo de oficina", "Terrenos", "Total"], r10.Complemento!.Filas.Select(f => f.Etiquetas[0]));
        Assert.Equal(Total(r10.Reporte!, "F-00-103"), r10.Complemento.Filas[^1].Valores[^1]);
        Assert.Equal(["13050501 Clientes nacionales", "Total"], r08.Complemento!.Filas.Select(f => f.Etiquetas[0]));
        Assert.True(r11.Complemento!.Vacio);

        var (e2, c2, _) = await ConsolidarAsync([Modulo.Consolidacion]);
        await using var _e2 = e2;
        var sinModulo = await R(e2, c2, "R10");
        Assert.Null(sinModulo.Complemento);
        Assert.Equal("Sin el módulo de activos fijos la nota no incluye los movimientos de BW (RF-30).", sinModulo.AvisoComplemento);
    }

    [Fact(DisplayName = "HU-47 CA1 y CA2: R15 a R20 tienen la estructura común; R20 resume el grupo en moneda local y en dólares")]
    [Trait("HU", "HU-47")]
    public async Task OlaTresYJunta()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        foreach (var id in new[] { "R15", "R16", "R17", "R18", "R19" })
        {
            Assert.Equal(["Cuenta", "Descripción"], (await R(e, c, id)).Reporte!.Encabezados);
        }

        Assert.Equal(Total((await R(e, c, "R02")).Reporte!, "F-00-401"), Total((await R(e, c, "R19")).Reporte!, "F-00-401"));
        var r20 = (await R(e, c, "R20")).Reporte!;
        Assert.Equal(["COP", "USD"], r20.Columnas.Select(x => x.Id));
        Assert.Equal(Total((await R(e, c, "R01", moneda: "COP")).Reporte!, "Total activo"), Total(r20, "Total activo", 0));
        Assert.Equal(Total((await R(e, c, "R01", moneda: "USD")).Reporte!, "Total activo"), Total(r20, "Total activo", 1));
        Assert.Equal(Total((await R(e, c, "R02", moneda: "USD")).Reporte!, "Utilidad neta del ejercicio"), Total(r20, "Utilidad neta del ejercicio", 1));
        Assert.All(CatalogoReportes.Todos.Where(r => r.Id[0] == 'R'), r => Assert.True(r.Disponible && r.EnUx11));
    }

    [Fact(DisplayName = "HU-49 CA1, CA3 y CA4: con Presupuesto el plan crea el modelo de planeación, UX-09 muestra «Modelo de presupuesto» y el presupuesto del ERP queda en el modelo")]
    [Trait("HU", "HU-49")]
    public async Task ModeloDePresupuesto()
    {
        var (e, _, ej) = await ConsolidarAsync();
        await using var _e = e;

        var paso = ej.Pasos.Single(p => p.Id == PasosGeneracion.Presupuesto);
        Assert.Equal(("Modelo de presupuesto", EstadoPaso.Completo), (paso.Nombre, paso.Estado));
        Assert.Contains(paso.Objetos, o => o is { Tipo: "Modelo", NombreTecnico: MotorPresupuesto.Modelo });
        Assert.Contains(paso.Objetos, o => o is { Tipo: "DataSource", NombreTecnico: "0FI_GL_14" });
        Assert.Contains(paso.Objetos, o => o.NombreTecnico == "P05_Formulario_de_captura_de_presupuesto.xlsx");
        var plan = e.Sap.Almacen.Registros.Where(r => r is { Modelo: MotorPresupuesto.Modelo, Entidad: "1000", Categoria: "PRESUP_ANUAL" }).Sum(r => r.Importe);
        Assert.Equal(SimulatedSapGateway.PlanErp("1000", 2026, e.Opciones).Where(x => x.Categoria == SimulatedSapGateway.CategoriaPlanErp).Sum(x => x.Importe), plan);
        Assert.NotEqual(0m, plan);
    }

    [Fact(DisplayName = "HU-49 CA1 y CA2: sin Presupuesto no hay modelo de planeación; el de planeación no toca la consolidación")]
    [Trait("HU", "HU-49")]
    public async Task SoloConElModulo()
    {
        var (e, c, ej) = await ConsolidarAsync([Modulo.Consolidacion]);
        await using var _e = e;
        Assert.DoesNotContain(ej.Pasos, p => p.Id == PasosGeneracion.Presupuesto);
        Assert.DoesNotContain(e.Sap.Almacen.Registros, r => r.Modelo == MotorPresupuesto.Modelo);

        var (e2, c2, _) = await ConsolidarAsync();
        await using var _e2 = e2;
        Assert.All(e2.Sap.Almacen.Registros.Where(r => r.Modelo == MotorPresupuesto.Modelo), r => Assert.Equal(MotorConsolidacion.MonedaLocal, r.Moneda));
        var consolidacion = e2.Bpc.ToList();
        var v = (await e2.ServicioPresupuesto.ObtenerAsync(c2.Id, "1000", "PRESUP_ANUAL", 2026))!;
        await e2.ServicioPresupuesto.GuardarAsync(c2.Id, "1000", "PRESUP_ANUAL", 2026, [new(v.Filas[0].Cuenta, v.Filas[0].Centro, ["1.000.000"])]);
        Assert.Equal(consolidacion, e2.Bpc);
    }

    [Fact(DisplayName = "HU-50 CA1, CA3 y HU-52 CA3: P05 trae cuentas, centros y meses; lo guardado queda en el modelo de planeación y se refleja en P01")]
    [Trait("HU", "HU-50")]
    [Trait("HU", "HU-52")]
    public async Task CapturaYP01()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        var v = (await e.ServicioPresupuesto.ObtenerAsync(c.Id, "1000", null, null))!;
        var ventas = v.Filas.Single(f => f.Cuenta == "PCCO_41200501");
        var antes = Total((await R(e, c, "P01", sociedad: "1000")).Reporte!, "F-00-401", 0);

        Assert.Equal(("PRESUP_ANUAL", 2026, false), (v.Version.Codigo, v.Anio, v.SinPresupuesto));
        Assert.Equal(12, ventas.Meses.Count);
        Assert.True(ventas.Meses[0] > 0m);
        var r = await e.ServicioPresupuesto.GuardarAsync(c.Id, "1000", "PRESUP_ANUAL", 2026, [new(ventas.Cuenta, ventas.Centro, ["1.000.000", .. ventas.Meses.Skip(1).Select(m => m.ToString(System.Globalization.CultureInfo.InvariantCulture))])]);

        Assert.Equal((12, 0, (string?)null), (r.Guardadas, r.Errores.Count, r.Error));
        Assert.Equal(1_000_000m, (await e.ServicioPresupuesto.ObtenerAsync(c.Id, "1000", "PRESUP_ANUAL", 2026))!.Filas.Single(f => f.Cuenta == ventas.Cuenta).Meses[0]);
        Assert.Equal(-1_000_000m, e.Sap.Almacen.Registros.Single(x => x is { Modelo: MotorPresupuesto.Modelo, Entidad: "1000", Cuenta: "PCCO_41200501", Periodo: "2026.01", Categoria: "PRESUP_ANUAL" }).Importe);
        Assert.Equal(antes - ventas.Meses[0] + 1_000_000m, Total((await R(e, c, "P01", sociedad: "1000")).Reporte!, "F-00-401", 0));
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Paso == ServicioPresupuesto.PasoGuardar);
    }

    [Fact(DisplayName = "HU-50 CA2 y CA4: solo se aceptan números (la celda inválida no se guarda); una versión sin datos muestra el estado vacío")]
    [Trait("HU", "HU-50")]
    public async Task SoloNumerosYVacio()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        var vacia = (await e.ServicioPresupuesto.ObtenerAsync(c.Id, "1000", "PROY_6_6", 2026))!;
        Assert.True(vacia.SinPresupuesto);
        Assert.Equal("Sin presupuesto para esta versión: ingresa los valores.", ServicioPresupuesto.MensajeVacio);
        var f = vacia.Filas[0];

        var r = await e.ServicioPresupuesto.GuardarAsync(c.Id, "1000", "PROY_6_6", 2026, [new(f.Cuenta, f.Centro, ["abc", "500"])]);

        Assert.Equal(new ErrorCeldaPresupuesto(1, 1, "Solo se aceptan números"), Assert.Single(r.Errores));
        var despues = (await e.ServicioPresupuesto.ObtenerAsync(c.Id, "1000", "PROY_6_6", 2026))!.Filas[0];
        Assert.Equal((0m, 500m), (despues.Meses[0], despues.Meses[1]));
    }

    [Fact(DisplayName = "HU-51 CA1, CA2 y CA3: la versión se elige de la lista de HU-12; lo guardado en 3+9 no cambia la anual; P01 lleva la versión")]
    [Trait("HU", "HU-51")]
    public async Task Versiones()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        var anual = (await e.ServicioPresupuesto.ObtenerAsync(c.Id, "1000", "PRESUP_ANUAL", 2026))!;
        var f = anual.Filas[0];

        await e.ServicioPresupuesto.GuardarAsync(c.Id, "1000", "PROY_3_9", 2026, [new(f.Cuenta, f.Centro, ["7"])]);

        Assert.Equal(VersionPresupuesto.Propuestas.Select(x => x.Codigo), anual.Versiones.Select(x => x.Codigo));
        Assert.Equal(f.Meses, (await e.ServicioPresupuesto.ObtenerAsync(c.Id, "1000", "PRESUP_ANUAL", 2026))!.Filas[0].Meses);
        Assert.Equal(7m, (await e.ServicioPresupuesto.ObtenerAsync(c.Id, "1000", "PROY_3_9", 2026))!.Filas[0].Meses[0]);
        var p01 = await R(e, c, "P01", sociedad: "1000", version: "PROY_3_9");
        Assert.Equal("PROY_3_9", p01.Parametros.Version);
        Assert.Equal(VersionPresupuesto.Propuestas.Count, p01.Versiones.Count);
    }

    [Fact(DisplayName = "HU-52 CA1 y CA2: P01 muestra presupuesto, ejecución, variación y % de ejecución; P02 a P04 sobre el modelo de planeación")]
    [Trait("HU", "HU-52")]
    public async Task ReportesDePresupuesto()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        var erp = SimulatedSapGateway.PlanErp("1000", 2026, e.Opciones);

        var p01 = (await R(e, c, "P01", sociedad: "1000")).Reporte!;
        var p02 = (await R(e, c, "P02", sociedad: "1000")).Reporte!;
        var p03 = (await R(e, c, "P03", sociedad: "1000")).Reporte!;
        var p04 = (await R(e, c, "P04", sociedad: "1000")).Reporte!;

        Assert.Equal(["Presupuesto", "Ejecución", "Variación", "% ejecución"], p01.Columnas.Select(x => x.Titulo));
        var ingresos = p01.Fila("F-00-401")!.Valores;
        var reales = -erp.Where(x => x.Categoria == SimulatedSapGateway.CategoriaReal && x.Cuenta.StartsWith('4')).Sum(x => x.Importe);
        Assert.Equal(reales, ingresos[1]);
        Assert.Equal(ingresos[1] - ingresos[0], ingresos[2]);
        Assert.Equal(MotorPresupuesto.PorcentajeEjecucion(ingresos[0], ingresos[1]), ingresos[3]);
        Assert.All(p02.Filas.Where(f => f.Tipo == TipoFila.Rubro), f => Assert.StartsWith("1000CC", f.Etiquetas[0], StringComparison.Ordinal));
        Assert.All(p03.Filas.Where(f => f.Tipo == TipoFila.Rubro), f => Assert.StartsWith("1000PC", f.Etiquetas[0], StringComparison.Ordinal));
        Assert.Equal(p01.Fila("Total")!.Valores[0], p02.Fila("Total")!.Valores[0] + p03.Fila("Total")!.Valores[0]);
        var proy = p04.Fila("F-00-401")!.Valores;
        Assert.Equal((proy[0] + proy[1], ingresos[0]), (proy[2], proy[3]));
    }

    [Fact(DisplayName = "HU-53 CA1 y CA3: A01 a A03 y D01, D02 leen BW; A01 y D01 muestran saldo inicial, movimientos y saldo final")]
    [Trait("HU", "HU-53")]
    public async Task ReportesDeBw()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        var a01 = (await R(e, c, "A01", sociedad: "1000")).Reporte!;
        var d01 = (await R(e, c, "D01", sociedad: "1000")).Reporte!;
        var a02 = (await R(e, c, "A02", sociedad: "1000")).Reporte!;
        var a03 = (await R(e, c, "A03", sociedad: "1000")).Reporte!;
        var d02 = (await R(e, c, "D02")).Reporte!;

        Assert.Equal(["Saldo inicial", "Adiciones", "Retiros", "Depreciación del periodo", "Saldo final"], a01.Columnas.Select(x => x.Titulo));
        Assert.All(a01.Filas, f => Assert.Equal(f.Valores.Take(4).Sum(), f.Valores[4]));
        Assert.Equal(87_167_250_000m, a01.Fila("Total")!.Valores[4]);
        Assert.Equal(["Saldo inicial", "Deterioro del periodo", "Recuperaciones", "Castigos", "Saldo final"], d01.Columnas.Select(x => x.Titulo));
        Assert.Equal(decimal.Round(-31_420_900_000m * 0.03m, 2), d01.Fila("13050501 Clientes nacionales")!.Valores[4]);
        Assert.Equal(a01.Fila("Total")!.Valores[3], a02.Fila("Total")!.Valores[0]);
        Assert.Equal(6, a03.Filas.Count(f => f.Tipo == TipoFila.Detalle));
        Assert.Equal(["1000", "2000", "3000"], d02.Filas.Select(f => f.Etiquetas[0]).Distinct());
        Assert.Contains(e.Sap.Llamadas, l => l.Operacion.StartsWith("Z_ACC_BW_LEER ZACC_AF", StringComparison.Ordinal) && l.Sistema == SistemaSap.Bw);
        Assert.DoesNotContain(e.Sap.Llamadas, l => l.Operacion.StartsWith("Z_ACC_BPC_LEER", StringComparison.Ordinal) && l.Sistema == SistemaSap.Bw);
    }

    [Fact(DisplayName = "HU-53 CA2 y HU-48: el catálogo muestra BW como fuente de A01 a D02 y el .zip trae los 34 reportes y plantillas")]
    [Trait("HU", "HU-53")]
    [Trait("HU", "HU-47")]
    public async Task CatalogoCompleto()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        var catalogo = (await e.ServicioReportes.CatalogoAsync(c.Id, null))!;
        var paquete = (await e.ServicioReportes.DescargarAsync(c.Id))!;

        Assert.Equal(34, catalogo.Reportes.Count);
        Assert.All(catalogo.Reportes.Where(f => f.Definicion.Id[0] is 'A' or 'D'), f => Assert.Equal(FuenteReporte.Bw, f.Definicion.Fuente));
        Assert.All(paquete.Generados, g => Assert.True(g.Exitoso, $"{g.Reporte}: {g.Mensaje}"));
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(paquete.Zip));
        Assert.Equal(34, zip.Entries.Count);
    }
}
