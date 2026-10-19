using System.Text;
using System.Text.Json;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-25")]
public class LectorArchivoNoSapTests
{
    private static readonly string[] NoSap = ["9000"];
    private static readonly IReadOnlySet<string> Mapeadas = new HashSet<string> { "11050501", "41200501" };

    internal static byte[] Csv(params string[] filas) =>
        Encoding.UTF8.GetBytes("sociedad;cuenta;periodo;tercero;ceco;cebe;importe\r\n" + string.Join("\r\n", filas) + "\r\n");

    private static ArchivoNoSap Leer(byte[] contenido, string sociedad = "9000") => LectorArchivoNoSap.Leer(contenido, "9000_2026_09.csv", sociedad, NoSap, Mapeadas);

    [Fact(DisplayName = "HU-25 CA1: un archivo válido trae sociedad, cuenta, periodo, tercero, CeCo, CeBe e importe")]
    public void Valido()
    {
        var a = Leer(Csv("9000;11050501;2026-09;800123456;CC100;PC10;12500000", "9000;41200501;2026.09;;;PC10;-48900000,50"));

        Assert.True(a.Valido);
        Assert.Equal(new FilaNoSap(2, "9000", "11050501", "2026-09", "800123456", "CC100", "PC10", 12_500_000m), a.Filas[0]);
        Assert.Equal(-48_900_000.50m, a.Filas[1].Importe);
        Assert.Equal(["2026-09"], a.Periodos);
        Assert.Equal(-36_400_000.50m, a.Total);
        Assert.Equal(["Sociedad", "Cuenta", "Periodo", "Tercero", "CeCo", "CeBe", "Importe"], LectorArchivoNoSap.Titulos);
    }

    [Fact(DisplayName = "HU-25 CA2: los errores se listan por fila, columna y motivo; una cuenta fuera del mapeo es «cuenta sin mapear»")]
    public void ErroresPorFila()
    {
        var a = Leer(Csv("9000;11050501;2026-09;;;;12.500", "9000;11050501;2026-09;;;;abc", "9000;11050501;2026-13;;;;1", "9000;9999;2026-09;;;;1"));

        Assert.False(a.Valido);
        Assert.Equal(3, a.TotalErrores);
        Assert.Contains(new ErrorArchivo(3, "importe", "«abc» no es un importe numérico."), a.Errores);
        Assert.Contains(new ErrorArchivo(4, "periodo", "«2026-13» no es un periodo válido; usa aaaa-mm (p. ej. 2026-09)."), a.Errores);
        var sinMapear = a.Errores.Single(x => x.Fila == 5);
        Assert.Equal("cuenta", sinMapear.Columna);
        Assert.Contains(LectorArchivoNoSap.CuentaSinMapear, sinMapear.Motivo, StringComparison.Ordinal);
        Assert.Single(a.Filas);
    }

    [Fact(DisplayName = "HU-25 CA4: el archivo debe ser de una sociedad del grupo registrada como no SAP (RN-07)")]
    public void SoloDelGrupo()
    {
        Assert.Equal(LectorArchivoNoSap.MensajeNoEsDelGrupo("D999"), Leer(Csv("D999;11050501;2026-09;;;;1"), "D999").Errores.Single().Motivo);

        var otra = Leer(Csv("D999;11050501;2026-09;;;;1"));
        Assert.Equal(new ErrorArchivo(2, "sociedad", LectorArchivoNoSap.MensajeNoEsDelGrupo("D999")), otra.Errores.Single());
    }

    [Fact(DisplayName = "HU-25: un archivo con columnas faltantes o en otra codificación se rechaza con el motivo")]
    public void FormatoInvalido()
    {
        var columnas = Leer(Encoding.UTF8.GetBytes("sociedad;cuenta;importe\r\n9000;11050501;1\r\n"));
        Assert.StartsWith("Faltan columnas en el encabezado: periodo, tercero, ceco, cebe.", columnas.Errores.Single().Motivo, StringComparison.Ordinal);

        var latin1 = Encoding.Latin1.GetBytes("sociedad;cuenta;periodo;tercero;ceco;cebe;importe\r\n9000;11050501;2026-09;Compañía;;;1\r\n");
        Assert.Equal(new ErrorArchivo(0, "archivo", "El archivo no está en UTF-8. Guárdalo como «CSV UTF-8» y vuelve a subirlo."), Leer(latin1).Errores.Single());

        Assert.Equal("El archivo no tiene filas de datos.", Leer(Encoding.UTF8.GetBytes("sociedad;cuenta\r\n")).Errores.Single().Motivo);
        Assert.Equal("La fila tiene 2 columnas; se esperan 7.", Leer(Csv("9000;1")).Errores.Single().Motivo);
    }

    [Fact(DisplayName = "HU-25: acepta coma o tabulador como separador y el encabezado sin importar mayúsculas")]
    public void Separadores()
    {
        var coma = LectorArchivoNoSap.Leer(Encoding.UTF8.GetBytes("Sociedad,Cuenta,Periodo,Tercero,CeCo,CeBe,Importe\n9000,11050501,2026-09,,,,1500.25\n"), "a.csv", "9000", NoSap, Mapeadas);
        Assert.Equal(1500.25m, Assert.Single(coma.Filas).Importe);

        var tab = LectorArchivoNoSap.Leer(Encoding.UTF8.GetBytes("sociedad\tcuenta\tperiodo\ttercero\tceco\tcebe\timporte\n9000\t11050501\t2026-09\t\t\t\t7\n"), "a.txt", "9000", NoSap, Mapeadas);
        Assert.True(tab.Valido);
    }
}

[Trait("HU", "HU-25")]
public class ServicioNoSapTests
{
    private static async Task<(Entorno, Cliente)> ConNoSapAsync()
    {
        var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: true);
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501", "11100501"]);
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-401", ["41200501"]);
        Assert.Equal(Pasos.NoSap, await e.ServicioMapeo.ContinuarAsync(c.Id));
        return (e, c);
    }

    [Fact(DisplayName = "HU-25 CA1 y CA3: un archivo válido queda pendiente; Confirmar lo carga a BW y marca la sociedad cargada")]
    public async Task ConfirmarCarga()
    {
        var (e, c) = await ConNoSapAsync();
        await using var _ = e;

        var a = await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "9000.csv", LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;CC100;PC10;100", "9000;41200501;2026-09;;;PC10;-40"));
        Assert.True(a.Valido);
        Assert.NotNull((await e.ServicioNoSap.ObtenerAsync(c.Id))!.Sociedades.Single().Pendiente);

        var r = await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000");

        Assert.True(r.Exitosa, r.Mensaje);
        var vista = (await e.ServicioNoSap.ObtenerAsync(c.Id))!;
        Assert.True(vista.TodasCargadas);
        Assert.Null(vista.Sociedades.Single().Pendiente);
        var carga = Assert.Single(await e.Cargas.ListarAsync(c.Id));
        Assert.Equal(("9000", "2026-09", 2, 60m), (carga.Sociedad, carga.Periodo, carga.Filas, carga.Total));
        var datos = e.Sap.Datos[$"{ServicioNoSap.TablaStaging}/9000"];
        Assert.Equal(2, JsonDocument.Parse(datos.Definicion).RootElement.GetProperty("filas").GetArrayLength());
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Paso == ServicioNoSap.PasoCarga && x.Resultado == ResultadoEjecucion.Exitoso);
    }

    [Fact(DisplayName = "HU-25 CA3: con errores no se puede confirmar y no se carga nada")]
    public async Task NoConfirmaConErrores()
    {
        var (e, c) = await ConNoSapAsync();
        await using var _ = e;
        await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "9000.csv", LectorArchivoNoSapTests.Csv("9000;51959501;2026-09;;;;1"));

        var r = await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000");

        Assert.False(r.Exitosa);
        Assert.Empty(await e.Cargas.ListarAsync(c.Id));
        Assert.Empty(e.Sap.Datos);
    }

    [Fact(DisplayName = "HU-25: una carga nueva del mismo periodo reemplaza la anterior y no duplica")]
    public async Task Reemplaza()
    {
        var (e, c) = await ConNoSapAsync();
        await using var _ = e;
        await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "v1.csv", LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;;;100", "9000;11050501;2026-09;;;;100"));
        await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000");

        await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "v2.csv", LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;;;150"));
        await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000");

        var carga = Assert.Single(await e.Cargas.ListarAsync(c.Id));
        Assert.Equal(("v2.csv", 1, 150m), (carga.Archivo, carga.Filas, carga.Total));
        Assert.Equal(1, JsonDocument.Parse(e.Sap.Datos[$"{ServicioNoSap.TablaStaging}/9000"].Definicion).RootElement.GetProperty("filas").GetArrayLength());
    }

    [Fact(DisplayName = "HU-25: si BW rechaza la carga no queda nada a medias y la sociedad sigue pendiente")]
    public async Task FallaSinParciales()
    {
        var (e, c) = await ConNoSapAsync();
        await using var _ = e;
        await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "9000.csv", LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;;;1"));
        e.Opciones.FallaEscritura.Add(SistemaSap.Bw);

        var r = await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000");

        Assert.False(r.Exitosa);
        Assert.Empty(await e.Cargas.ListarAsync(c.Id));
        Assert.Empty(e.Sap.Datos);
        Assert.False((await e.ServicioNoSap.ObtenerAsync(c.Id))!.TodasCargadas);
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Paso == ServicioNoSap.PasoCarga && x.Resultado == ResultadoEjecucion.Fallido);
    }

    [Fact(DisplayName = "HU-25: la validación vence a los 30 minutos y los importes no se guardan en la base local")]
    public async Task PendienteVence()
    {
        var (e, c) = await ConNoSapAsync();
        await using var _ = e;
        await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "9000.csv", LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;;;123456789"));

        e.Reloj.Ahora += CargasPendientes.Vigencia + TimeSpan.FromSeconds(1);

        Assert.False((await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000")).Exitosa);
        var bytes = await File.ReadAllBytesAsync(e.Base.RutaArchivo);
        Assert.DoesNotContain("123456789", Encoding.Latin1.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-25 CA5 y HU-13 CA4: sin la carga no SAP el plan la pide; con ella aparece el paso que la lleva a la bodega")]
    public async Task PlanConNoSap()
    {
        var (e, c) = await ConNoSapAsync();
        await using var _ = e;

        var sin = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;
        Assert.Equal(Pasos.NoSap, Assert.Single(sin.Faltantes).Pantalla);
        Assert.Null(await e.ServicioNoSap.ContinuarAsync(c.Id));

        await e.ServicioNoSap.ValidarAsync(c.Id, "9000", "9000.csv", LectorArchivoNoSapTests.Csv("9000;11050501;2026-09;;;;1"));
        await e.ServicioNoSap.ConfirmarAsync(c.Id, "9000");
        Assert.Equal(Pasos.Plan, await e.ServicioNoSap.ContinuarAsync(c.Id));

        var plan = (await e.ServicioPlan.ObtenerAsync(c.Id))!.Plan;
        var paso = plan.Pasos.Single(p => p.Id == PasosGeneracion.NoSap);
        Assert.Contains(paso.Objetos, o => o.NombreTecnico == GeneradorNoSap.DataSource);
        Assert.Contains("HU-36", paso.Objetos[^1].Nota, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-25: las cuentas de una sociedad no SAP sin plan se validan contra el plan de su matriz")]
    public void PlanDeLaMatriz()
    {
        Sociedad[] todas =
        [
            new() { Codigo = "1000", PlanCuentas = "PCCO", Metodo = MetodoConsolidacion.Matriz },
            new() { Codigo = "9000", EnSap = false, Matriz = "1000" },
            new() { Codigo = "9100", EnSap = false, Matriz = "1000", PlanCuentas = "PCNS" },
        ];

        Assert.Equal("PCCO", ServicioNoSap.PlanDeCuentas(todas[1], todas));
        Assert.Equal("PCNS", ServicioNoSap.PlanDeCuentas(todas[2], todas));
    }
}
