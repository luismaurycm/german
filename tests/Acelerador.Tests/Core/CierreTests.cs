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

/// <summary>HU-37 a HU-41: C01, C03, C04, asientos de ajuste y estados de trabajo, contra el simulador.</summary>
public class CierreTests
{
    internal static async Task<(Entorno, Cliente, EjecucionPlan)> ConsolidarAsync(Action<OpcionesSimulador>? antes = null)
    {
        var e = await Entorno.CrearAsync();
        var c = await e.ClienteListoParaConsolidarAsync();
        antes?.Invoke(e.Opciones);
        var ej = await e.GenerarAsync(c.Id);
        return (e, c, ej);
    }

    /// <summary>Diferencia intercompañía de 5.000.000 entre 1000 y 2000 que no descuadra a 2000.</summary>
    internal static void DiferenciaIc(OpcionesSimulador o)
    {
        o.AjustesSaldo.Add("2000:22050501:5000000:1000");
        o.AjustesSaldo.Add("2000:11050501:-5000000");
    }

    private static RegistroBpc R(string ent, string cuenta, string intco, decimal v) => new("CONSOLIDACION", ent, cuenta, intco, "2026.09", "COP", "F_CLO", "INPUT", v);

    private static LineaAsientoTexto L(string soc, string cuenta, string? debito, string? credito, string intco = "I_NONE") => new(soc, cuenta, intco, debito, credito, "Ajuste");

    [Fact(DisplayName = "HU-37 CA1: C03 muestra por sociedad la diferencia de balance, la de utilidad y resultado, las cuentas mapeadas y si se importó")]
    [Trait("HU", "HU-37")]
    public async Task C03Normal()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        var v = (await e.ServicioControles.C03Async(c.Id, "2026-09"))!;

        Assert.Null(v.Error);
        Assert.Equal(["1000", "2000", "3000"], v.Filas.Select(f => f.Sociedad.Codigo));
        Assert.All(v.Filas, f => Assert.Equal((0m, 0m, 0, true), (f.Validacion.DiferenciaBalance, f.Validacion.DiferenciaResultado, f.Validacion.CuentasSinMapear.Count, f.Validacion.Importada)));
    }

    [Fact(DisplayName = "HU-37 CA2: una sociedad descuadrada queda «No importada» en C03 con su diferencia")]
    [Trait("HU", "HU-37")]
    public async Task C03NoImportada()
    {
        var (e, c, _) = await ConsolidarAsync(o => o.AjustesSaldo.Add("2000:11050501:5000000"));
        await using var _e = e;

        var fila = (await e.ServicioControles.C03Async(c.Id, "2026-09"))!.Filas.Single(f => f.Sociedad.Codigo == "2000");

        Assert.False(fila.Validacion.Importada);
        Assert.Equal(5_000_000m, fila.Validacion.DiferenciaBalance);
    }

    [Fact(DisplayName = "HU-37 CA3 y CA4: sin importaciones C03 está vacío; sin conexión a BPC muestra el error")]
    [Trait("HU", "HU-37")]
    public async Task C03VacioYError()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        Assert.True((await e.ServicioControles.C03Async(c.Id, "2026-12"))!.Vacio);
        Assert.Equal("Aún no hay importaciones en el periodo elegido.", ServicioControles.SinImportaciones);
        e.Opciones.SistemasConFallo.Add(SistemaSap.Bpc);
        var v = (await e.ServicioControles.C03Async(c.Id, "2026-09"))!;
        Assert.NotNull(v.Error);
        Assert.False(v.Vacio);
    }

    [Fact(DisplayName = "HU-38 CA1: C01 muestra por pareja de sociedades y cuentas lo que registra cada parte y la diferencia")]
    [Trait("HU", "HU-38")]
    public async Task C01Parejas()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        var v = (await e.ServicioControles.C01Async(c.Id, "2026-09", "COP"))!;

        Assert.Null(v.Error);
        var balance = v.Parejas.Single(p => p is { Sociedad: "1000", Contraparte: "2000", Naturaleza: ReglasConciliacion.Balance });
        Assert.Equal(["PCCO_13050501"], balance.CuentasSociedad);
        Assert.Equal(["PCCO_22050501"], balance.CuentasContraparte);
        Assert.Equal((1_000_000_000m, 1_000_000_000m, 0m), (balance.RegistraSociedad, balance.RegistraContraparte, balance.Diferencia));
        Assert.Contains(v.Parejas, p => p is { Sociedad: "1000", Contraparte: "2000", Naturaleza: ReglasConciliacion.Resultados, Diferencia: 0m });
        Assert.Contains(v.Parejas, p => p is { Sociedad: "1000", Contraparte: "3000", RegistraSociedad: 200_000_000m, Diferencia: 0m });
        Assert.DoesNotContain(v.Parejas, p => p.CuentasSociedad.Contains("PCCO_12050501"));
        Assert.Equal(50_000m, (await e.ServicioControles.C01Async(c.Id, "2026-09", "USD"))!.Parejas.Single(p => p.Contraparte == "3000").RegistraContraparte);
    }

    [Fact(DisplayName = "HU-38 CA2: la pareja con una diferencia sobre el umbral se marca «Supera el umbral»")]
    [Trait("HU", "HU-38")]
    public async Task C01SuperaUmbral()
    {
        var (e, c, _) = await ConsolidarAsync(DiferenciaIc);
        await using var _e = e;

        var v = (await e.ServicioControles.C01Async(c.Id, "2026-09", "COP"))!;

        var pareja = v.Parejas.Single(p => p is { Sociedad: "1000", Contraparte: "2000", Naturaleza: ReglasConciliacion.Balance });
        Assert.Equal(5_000_000m, pareja.Diferencia);
        Assert.True(ReglasConciliacion.SuperaUmbral(pareja, v.Umbral));
        Assert.All(v.Parejas.Where(p => p != pareja), p => Assert.False(ReglasConciliacion.SuperaUmbral(p, v.Umbral)));
    }

    [Theory(DisplayName = "HU-38 CA2: solo se marca la diferencia que supera el umbral de 1.000,00 (999,99 · 1.000,00 · 1.000,01)")]
    [Trait("HU", "HU-38")]
    [InlineData("999.99", false)]
    [InlineData("1000.00", false)]
    [InlineData("1000.01", true)]
    public void UmbralDeConciliacion(string diferencia, bool supera)
    {
        var d = decimal.Parse(diferencia, System.Globalization.CultureInfo.InvariantCulture);
        var parejas = ReglasConciliacion.Parejas([R("1000", "CXC", "I_2000", 1_000_000m + d), R("2000", "CXP", "I_1000", -1_000_000m)],
            new HashSet<string> { "1000", "2000" }, new Dictionary<string, ClaseCuenta>(), new HashSet<string>(), "COP");

        var p = Assert.Single(parejas);
        Assert.Equal(d, p.Diferencia);
        Assert.Equal(supera, ReglasConciliacion.SuperaUmbral(p, ReglasConciliacion.UmbralProvisional));
    }

    [Fact(DisplayName = "HU-38 CA3: C01 solo incluye sociedades del grupo (RN-07): ni terceros ni sociedades fuera del perímetro")]
    [Trait("HU", "HU-38")]
    public async Task C01SoloGrupo()
    {
        var parejas = ReglasConciliacion.Parejas([R("1000", "CXC", "I_T9001", 500m), R("1000", "CXC", "I_2000", 10m), R("2000", "CXP", "I_1000", -10m)],
            new HashSet<string> { "1000", "2000" }, new Dictionary<string, ClaseCuenta>(), new HashSet<string>(), "COP");
        Assert.Equal(("1000", "2000"), (Assert.Single(parejas).Sociedad, parejas[0].Contraparte));

        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        await e.ServicioParticipaciones.GuardarAsync(c.Id, "2026-09", [new("3000", "0", "0", "0", nameof(MetodoConsolidacion.FueraDelPerimetro))]);
        Assert.DoesNotContain((await e.ServicioControles.C01Async(c.Id, "2026-09", "COP"))!.Parejas, p => p.Contraparte == "3000");
    }

    [Fact(DisplayName = "HU-38 CA4: C01 se consulta antes de la consolidación del periodo")]
    [Trait("HU", "HU-38")]
    public async Task C01AntesDeConsolidar()
    {
        var (e, c, ej) = await ConsolidarAsync(o => o.FallaLogica.Add("Eliminación intercompañía G_1000 2026-09"));
        await using var _e = e;

        Assert.Equal(PasosGeneracion.Consolidacion, ej.PasoConError!.Id);
        Assert.DoesNotContain(e.Sap.Almacen.Registros, r => r.Origen == MotorConsolidacion.OrigenEliminacionIc);
        Assert.Equal(3, (await e.ServicioControles.C01Async(c.Id, "2026-09", "COP"))!.Parejas.Count);
    }

    [Fact(DisplayName = "HU-39 CA1 y CA4: el asiento cuadrado se guarda con número, usuario y fecha y queda en la consolidación del grupo")]
    [Trait("HU", "HU-39")]
    public async Task GuardarAsiento()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        var r = await e.ServicioAsientos.GuardarAsync(c.Id, "2026-09", "COP", "Reclasificación", [L("1000", "PCCO_11050501", "1.000.000,00", null), L("1000", "PCCO_13050501", null, "1000000", "I_2000")]);

        Assert.Equal("AJ-0001", r.Numero);
        var v = (await e.ServicioAsientos.ObtenerAsync(c.Id, "2026-09", "COP"))!;
        var a = Assert.Single(v.Asientos);
        Assert.Equal(("AJ-0001", "G_1000", "COP", "Reclasificación", @"CM\consultor1", e.Reloj.Ahora, 1_000_000m, 2),
            (a.Numero, a.Grupo, a.Moneda, a.Descripcion, a.Usuario, a.Fecha, a.Valor, a.Lineas.Count));
        Assert.Equal(new LineaAsiento("1000", "PCCO_13050501", "I_2000", 0m, 1_000_000m, "Ajuste"), a.Lineas[1]);
        Assert.Equal(-1_000_000m, e.Sap.Almacen.Registros.Single(x => x is { Origen: MotorConsolidacion.OrigenAjuste, Cuenta: "PCCO_13050501" } && x.Grupo == "G_1000").Importe);
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Paso == ServicioAsientos.PasoGuardar && x.Objetos.StartsWith("AJ-0001", StringComparison.Ordinal));
        Assert.Equal("AJ-0002", (await e.ServicioAsientos.GuardarAsync(c.Id, "2026-09", "USD", null, [L("1000", "ZACC_DIF_IC", "5", null), L("2000", "ZACC_DIF_IC", null, "5")])).Numero);
    }

    [Theory(DisplayName = "HU-39 CA2: solo se guarda el asiento con débitos iguales a créditos; si no, «Descuadrado» con la diferencia (RN-15)")]
    [Trait("HU", "HU-39")]
    [InlineData("1.000.000,00", "999.999,99", "Descuadrado: 0,01")]
    [InlineData("1.000.000,00", "1.000.000,01", "Descuadrado: 0,01")]
    [InlineData("1.000.000", "900.000", "Descuadrado: 100.000,00")]
    [InlineData("1.000.000,00", "1000000", "Cuadrado")]
    public async Task SoloAsientosCuadrados(string debito, string credito, string indicador)
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        LineaAsientoTexto[] lineas = [L("1000", "PCCO_11050501", debito, null), L("2000", "PCCO_11050501", null, credito)];

        var v = (await e.ServicioAsientos.ObtenerAsync(c.Id, "2026-09", "COP"))!;
        var validado = ServicioAsientos.Validar(v, lineas);
        var r = await e.ServicioAsientos.GuardarAsync(c.Id, "2026-09", "COP", null, lineas);

        Assert.Equal(indicador, ReglasAsientos.Indicador(validado));
        Assert.Equal(indicador == "Cuadrado", validado.Cuadra);
        Assert.Equal(indicador == "Cuadrado", r.Numero is not null);
        Assert.Equal(indicador == "Cuadrado" ? 1 : 0, e.Sap.Almacen.Asientos.Count);
    }

    [Fact(DisplayName = "HU-39: un asiento sin líneas muestra «Aún no hay líneas: usa Agregar línea» y no se guarda")]
    [Trait("HU", "HU-39")]
    public void AsientoVacio()
    {
        var a = ReglasAsientos.Validar([new(null, null, null, null, null, null)], new HashSet<string> { "1000" }, new HashSet<string> { "C" }, new HashSet<string>(), "2026-09");

        Assert.Equal((0, false), (a.LineasEscritas, a.Guardable));
        Assert.Equal("Aún no hay líneas: usa Agregar línea.", ReglasAsientos.Vacio);
    }

    [Fact(DisplayName = "HU-39 CA3 y HU-40 CA2 y CA3: no se aceptan líneas de la sociedad bloqueada; las de las demás sí")]
    [Trait("HU", "HU-39")]
    [Trait("HU", "HU-40")]
    public async Task AsientoConSociedadBloqueada()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        Assert.True((await e.ServicioCierre.CambiarBloqueoAsync(c.Id, "2026-09", "2000", bloquear: true)).Exitosa);

        var r = await e.ServicioAsientos.GuardarAsync(c.Id, "2026-09", "COP", null, [L("1000", "PCCO_11050501", "10", null), L("2000", "PCCO_11050501", null, "10")]);

        Assert.Null(r.Numero);
        var error = Assert.Single(r.Validado.Errores);
        Assert.Equal((2, ReglasAsientos.Sociedad), (error.Linea, error.Dato));
        Assert.Equal("La sociedad 2000 está bloqueada en 2026-09: sus líneas no se aceptan hasta que se desbloquee (RN-17).", error.Motivo);
        Assert.Equal("AJ-0001", (await e.ServicioAsientos.GuardarAsync(c.Id, "2026-09", "COP", null, [L("1000", "PCCO_11050501", "10", null), L("3000", "PCPA_1010", null, "10")])).Numero);
        Assert.DoesNotContain((await e.ServicioCierre.ObtenerAsync(c.Id, "2026-10"))!.Filas, f => f.Bloqueada);
    }

    [Fact(DisplayName = "HU-40 CA2: una sociedad bloqueada no admite reprocesos del periodo y conserva sus datos (RN-17)")]
    [Trait("HU", "HU-40")]
    public async Task SinReprocesos()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;
        await e.ServicioCierre.CambiarBloqueoAsync(c.Id, "2026-09", "2000", bloquear: true);
        var antes = e.Sap.Almacen.Registros.Where(r => r.Entidad == "2000" && r.Grupo.Length == 0).ToList();
        e.Opciones.AjustesSaldo.Add("2000:11050501:5000000");

        var ej = await e.GenerarAsync(c.Id);

        Assert.Equal(EstadoEjecucion.Completa, ej.Estado);
        Assert.Contains("2000 está bloqueada en 2026-09: no se reimporta; se conservan sus datos (RN-17).", ej.Pasos.Single(p => p.Id == PasosGeneracion.Importacion).Avisos);
        Assert.Contains(ej.Pasos.Single(p => p.Id == PasosGeneracion.Conversion).Avisos, a => a.StartsWith("2000 está bloqueada en 2026-09", StringComparison.Ordinal));
        Assert.Equal(antes.OrderBy(r => r.ToString()), e.Sap.Almacen.Registros.Where(r => r.Entidad == "2000" && r.Grupo.Length == 0).OrderBy(r => r.ToString()));
        Assert.True(e.Sap.Almacen.Validaciones.Single(v => v.Sociedad == "2000").Importada);
        Assert.Equal(3, ej.SociedadesConsolidadas);
    }

    [Fact(DisplayName = "HU-40 CA2: una sociedad no SAP bloqueada no admite cargas del periodo (RN-17)")]
    [Trait("HU", "HU-40")]
    public async Task SinCargas()
    {
        var (e, c, _) = await PropiedadTests.ConsolidarAsync(conColigada: true);
        await using var _e = e;
        await e.ServicioCierre.CambiarBloqueoAsync(c.Id, "2026-09", "9000", bloquear: true);

        await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "9000.csv", LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;;;1", "9000;31050501;2026-09;;;;-1"));
        var r = await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000");

        Assert.False(r.Exitosa);
        Assert.Equal("La sociedad 9000 está bloqueada en 2026-09: no admite cargas hasta que se desbloquee (RN-17).", r.Mensaje);
        await e.ServicioCierre.CambiarBloqueoAsync(c.Id, "2026-09", "9000", bloquear: false);
        Assert.True((await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000")).Exitosa);
    }

    [Fact(DisplayName = "HU-40 CA1, CA3 y CA4: bloquear y desbloquear solo afecta a esa sociedad y queda auditado con usuario y fecha")]
    [Trait("HU", "HU-40")]
    public async Task BloqueoAuditado()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        await e.ServicioCierre.CambiarBloqueoAsync(c.Id, "2026-09", "2000", bloquear: true);
        Assert.Equal(["2000"], (await e.ServicioCierre.ObtenerAsync(c.Id, "2026-09"))!.Filas.Where(f => f.Bloqueada).Select(f => f.Sociedad.Codigo));
        e.Reloj.Ahora = e.Reloj.Ahora.AddHours(2);
        var r = await e.ServicioCierre.CambiarBloqueoAsync(c.Id, "2026-09", "2000", bloquear: false);

        Assert.Equal("2000 quedó desbloqueada en 2026-09.", r.Mensaje);
        Assert.DoesNotContain((await e.ServicioCierre.ObtenerAsync(c.Id, "2026-09"))!.Filas, f => f.Bloqueada);
        var auditoria = e.Sap.Almacen.Auditoria.Where(a => a.Modelo == LogicaBpcReferencia.EstadosTrabajo).ToList();
        Assert.Equal([("2000", "Abierta", "Bloqueada", e.Reloj.Ahora.AddHours(-2)), ("2000", "Bloqueada", "Abierta", e.Reloj.Ahora)],
            auditoria.Select(a => (a.Sociedad, a.Anterior, a.Nuevo, a.Fecha)));
        Assert.All(auditoria, a => Assert.Equal(@"CM\consultor1", a.Usuario));
        Assert.Equal(2, (await e.Registro.ListarAsync(c.Id)).Count(x => x.Paso is ServicioCierre.PasoBloquear or ServicioCierre.PasoDesbloquear));
    }

    [Fact(DisplayName = "HU-41 CA1: C04 muestra por sociedad carga, cuadre, conciliación, conversión, consolidación y reportes, en ese orden")]
    [Trait("HU", "HU-41")]
    public async Task MatrizDelCierre()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        var v = (await e.ServicioCierre.ObtenerAsync(c.Id, "2026-09"))!;

        Assert.Equal(["Carga", "Cuadre", "Conciliación IC", "Conversión", "Consolidación", "Reportes"], ReglasCierreMensual.Pasos);
        Assert.True(v.Iniciado);
        Assert.Null(v.Aviso);
        Assert.All(v.Filas, f => Assert.Equal(
            [EstadoCierre.Completo, EstadoCierre.Completo, EstadoCierre.Completo, EstadoCierre.Completo, EstadoCierre.Completo, EstadoCierre.Pendiente],
            f.Pasos.Select(p => p.Estado)));
    }

    [Fact(DisplayName = "HU-41 CA2: un paso con error enlaza a la hoja que explica la causa (C03 para el cuadre, C01 para la conciliación)")]
    [Trait("HU", "HU-41")]
    public async Task ErrorConCausa()
    {
        var (e, c, _) = await ConsolidarAsync(o =>
        {
            o.AjustesSaldo.Add("3000:1010:1000");
            DiferenciaIc(o);
        });
        await using var _e = e;

        var v = (await e.ServicioCierre.ObtenerAsync(c.Id, "2026-09"))!;

        var f3000 = v.Filas.Single(f => f.Sociedad.Codigo == "3000").Pasos;
        Assert.Equal((EstadoCierre.Completo, new CeldaCierre(EstadoCierre.ConError, "C03"), EstadoCierre.Pendiente), (f3000[0].Estado, f3000[1], f3000[2].Estado));
        Assert.Equal(new CeldaCierre(EstadoCierre.ConError, "C01"), v.Filas.Single(f => f.Sociedad.Codigo == "2000").Pasos[2]);
        Assert.Equal("1 sociedad no cuadra. 3000 no se importó; revisa C03 Validación de cuadre antes de consolidar.", v.Aviso);
    }

    [Fact(DisplayName = "HU-41 CA2: si la consolidación falla, el paso queda con error y enlaza a la ejecución")]
    [Trait("HU", "HU-41")]
    public async Task ConsolidacionConError()
    {
        var (e, c, _) = await ConsolidarAsync(o => o.FallaLogica.Add("Eliminación intercompañía G_1000 2026-09"));
        await using var _e = e;

        var v = (await e.ServicioCierre.ObtenerAsync(c.Id, "2026-09"))!;

        Assert.All(v.Filas, f => Assert.Equal(new CeldaCierre(EstadoCierre.ConError, ReglasCierreMensual.Ejecucion), f.Pasos[4]));
        Assert.All(v.Filas, f => Assert.Equal(EstadoCierre.Completo, f.Pasos[3].Estado));
    }

    [Fact(DisplayName = "HU-41 CA4: si el cierre del periodo no ha iniciado se muestra «El cierre de 2026-12 aún no ha iniciado»")]
    [Trait("HU", "HU-41")]
    public async Task NoIniciado()
    {
        var (e, c, _) = await ConsolidarAsync();
        await using var _e = e;

        var v = (await e.ServicioCierre.ObtenerAsync(c.Id, "2026-12"))!;

        Assert.False(v.Iniciado);
        Assert.Equal("El cierre de 2026-12 aún no ha iniciado.", ReglasCierreMensual.NoIniciado(v.Contexto.Periodo));
    }

    [Fact(DisplayName = "HU-37 a HU-41: el plan configura los estados de trabajo y los asientos y sube C01, C03, C04 y Asientos a BPC")]
    [Trait("HU", "HU-40")]
    [Trait("HU", "HU-39")]
    [Trait("HU", "HU-37")]
    [Trait("HU", "HU-38")]
    [Trait("HU", "HU-41")]
    public async Task PlantillasDelCierre()
    {
        var (e, _, ej) = await ConsolidarAsync();
        await using var _e = e;

        var objetos = ej.Pasos.Single(p => p.Id == PasosGeneracion.ModeloBpc).Objetos.Where(o => o.Grupo == GeneradorCierre.Grupo).ToList();
        Assert.Equal([GeneradorCierre.EstadosTrabajo, GeneradorCierre.Asientos, .. GeneradorCierre.Plantillas.Select(p => p.Archivo)], objetos.Select(o => o.NombreTecnico));
        foreach (var (id, archivo, _) in GeneradorCierre.Plantillas)
        {
            var subida = e.Sap.Objetos.Single(o => o.Operacion.NombreTecnico == archivo);
            var contenido = Convert.FromBase64String(JsonDocument.Parse(subida.Operacion.Definicion).RootElement.GetProperty("contenido").GetString()!);
            using var doc = SpreadsheetDocument.Open(new MemoryStream(contenido), false);
            Assert.Equal(id, doc.WorkbookPart!.Workbook.Sheets!.Elements<Sheet>().Single().Name!.Value);
        }
    }
}
