using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-11")]
public class ReglasMapeoTests
{
    private static readonly CuentaErp[] Cuentas =
    [
        new("1105", "Caja", "Disponible", 100m),
        new("1110", "Bancos", "Disponible", 250.5m),
        new("1305", "Clientes", "Deudores", 900m),
        new("2205", "Proveedores", "Proveedores", -300m),
    ];

    [Fact(DisplayName = "HU-11 CA2: el valor del rubro es la suma de sus cuentas mapeadas (RN-10)")]
    public void ValorRubro()
    {
        var cruzadas = ReglasMapeo.Cruzar(Cuentas, [new("PCCO", "1105", "F-00-101"), new("PCCO", "1110", "F-00-101"), new("PCCO", "1305", "F-00-102")]);

        Assert.Equal(350.5m, ReglasMapeo.ValorRubro(cruzadas, "F-00-101"));
        Assert.Equal(900m, ReglasMapeo.ValorRubro(cruzadas, "F-00-102"));
        Assert.Equal(0m, ReglasMapeo.ValorRubro(cruzadas, "F-00-201"));
        Assert.Equal(2, ReglasMapeo.CuentasDelRubro(cruzadas, "F-00-101"));
    }

    [Fact(DisplayName = "HU-11 CA3: el contador cuenta las cuentas sin mapear y el filtro «Solo sin mapear» las lista")]
    public void SinMapear()
    {
        var cruzadas = ReglasMapeo.Cruzar(Cuentas, [new("PCCO", "1105", "F-00-101")]);

        Assert.Equal(3, ReglasMapeo.SinMapear(cruzadas));
        Assert.Equal(["1110", "1305", "2205"], ReglasMapeo.Filtrar(cruzadas, soloSinMapear: true).Select(c => c.Cuenta.Numero));
        Assert.Equal(4, ReglasMapeo.Filtrar(cruzadas, soloSinMapear: false).Count);
    }

    [Theory(DisplayName = "HU-11 CA5: el mensaje dice cuántas asignaciones no se guardaron")]
    [InlineData(1, "No se pudo guardar el mapeo. La asignación de 1 cuenta no se guardó. Vuelve a asignarlas.")]
    [InlineData(3, "No se pudo guardar el mapeo. La asignación de 3 cuentas no se guardó. Vuelve a asignarlas.")]
    public void MensajeNoGuardadas(int n, string esperado) => Assert.Equal(esperado, ReglasMapeo.MensajeNoGuardadas(n));

    [Fact(DisplayName = "HU-11: el catálogo de rubros tiene códigos únicos e incluye los del prototipo")]
    public void Catalogo()
    {
        Assert.Equal(CatalogoRubros.Todos.Count, CatalogoRubros.Todos.Select(r => r.Codigo).Distinct().Count());
        foreach (var codigo in new[] { "F-00-101", "F-00-102", "F-00-103", "F-00-201", "F-00-202" })
        {
            Assert.NotNull(CatalogoRubros.Buscar(codigo));
        }
    }
}

[Trait("HU", "HU-11")]
public class ServicioMapeoTests
{
    [Fact(DisplayName = "HU-11 CA1 y CA4: las cuentas se leen del ERP por la conexión de HU-03 con el plan de la sociedad (RN-05)")]
    public async Task CuentasDelErpPorPlan()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();

        var colombia = (await e.ServicioMapeo.ObtenerAsync(c.Id, "2000", null, false))!;
        var panama = (await e.ServicioMapeo.ObtenerAsync(c.Id, "3000", null, false))!;

        Assert.Null(colombia.Error);
        Assert.Equal("PCCO", colombia.Sociedad!.PlanCuentas);
        Assert.All(colombia.Cuentas, x => Assert.Equal(8, x.Cuenta.Numero.Length));
        Assert.Equal(SimulatedSapGateway.Cuentas.Count(x => x.Plan == "PCCO"), colombia.TotalCuentas);
        Assert.Equal("COP", colombia.Moneda);
        Assert.Equal(["1010", "1020", "1200", "1500", "2000", "2100", "3000", "4000", "5000"], panama.Cuentas.Select(x => x.Cuenta.Numero));
        Assert.Equal("USD", panama.Moneda);
        Assert.Contains(e.Sap.Llamadas, l => l.Operacion == "BAPI_GL_ACC_GETLIST" && l.Sistema == SistemaSap.Erp);
        Assert.DoesNotContain(e.Sap.Llamadas, l => l.EsEscritura);
        Assert.DoesNotContain(colombia.Sociedades, s => s.Codigo == "9000");
    }

    [Fact(DisplayName = "HU-11 CA4: una cuenta nueva en el ERP aparece al recargar")]
    public async Task CuentaNuevaDelErp()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();
        e.Opciones.CuentasAdicionales.Add(new CuentaSimulada { Plan = "PCCO", Numero = "11990501", Descripcion = "Caja menor", Nodo = "Disponible", Saldo = 5_000_000m });

        var v = (await e.ServicioMapeo.ObtenerAsync(c.Id, "1000", null, false))!;

        Assert.Contains(v.Cuentas, x => x.Cuenta.Numero == "11990501" && x.Rubro is null);
    }

    [Fact(DisplayName = "HU-11 CA1 y CA2: las cuentas marcadas se asignan al rubro y la vista previa suma sus saldos (RN-10)")]
    public async Task AsignarYSumar()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();

        var r = await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501", "11100501"]);
        var v = (await e.ServicioMapeo.ObtenerAsync(c.Id, "1000", "F-00-101", false))!;

        Assert.Equal(new ResultadoMapeo(2, null), r);
        Assert.Equal(152_300_000m + 12_480_000_000m, v.ValorRubro);
        Assert.Equal(2, v.CuentasDelRubro);
        Assert.Equal(["11050501", "11100501"], v.Cuentas.Where(x => x.Rubro == "F-00-101").Select(x => x.Cuenta.Numero));

        // El mapeo es del plan: la filial 2000 (mismo plan PCCO) ve las mismas cuentas mapeadas con sus propios saldos.
        var filial = (await e.ServicioMapeo.ObtenerAsync(c.Id, "2000", "F-00-101", false))!;
        Assert.Equal(2, filial.CuentasDelRubro);
        Assert.Equal(decimal.Round((152_300_000m + 12_480_000_000m) * 0.35m, 2), filial.ValorRubro);
    }

    [Fact(DisplayName = "HU-11 CA3: el contador baja al mapear y el filtro «Solo sin mapear» deja solo las pendientes")]
    public async Task ContadorYFiltro()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();
        var antes = (await e.ServicioMapeo.ObtenerAsync(c.Id, "1000", null, false))!;
        Assert.True(antes.SinMapeos);

        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-102", ["13050501"]);
        var despues = (await e.ServicioMapeo.ObtenerAsync(c.Id, "1000", null, soloSinMapear: true))!;

        Assert.Equal(antes.SinMapear - 1, despues.SinMapear);
        Assert.False(despues.SinMapeos);
        Assert.Equal(despues.SinMapear, despues.Cuentas.Count);
        Assert.DoesNotContain(despues.Cuentas, x => x.Cuenta.Numero == "13050501");
    }

    [Fact(DisplayName = "HU-11: quitar una cuenta de su rubro la deja sin mapear")]
    public async Task Quitar()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501", "11100501"]);

        Assert.Equal(1, (await e.ServicioMapeo.QuitarAsync(c.Id, "1000", ["11100501"])).Guardadas);

        var v = (await e.ServicioMapeo.ObtenerAsync(c.Id, "1000", "F-00-101", false))!;
        Assert.Equal(152_300_000m, v.ValorRubro);
        Assert.Null(v.Cuentas.First(x => x.Cuenta.Numero == "11100501").Rubro);
    }

    [Fact(DisplayName = "HU-11 CA1 y RN-05: no se asigna una cuenta de otro plan ni sin marcar cuentas")]
    public async Task ValidaCuentas()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();

        Assert.Equal(ReglasMapeo.MensajeSinCuentas, (await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", [])).Error);
        Assert.Equal("La cuenta 1010 no está en el plan de cuentas PCCO.", (await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["1010"])).Error);
        Assert.Equal("El rubro X-99 no existe.", (await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "X-99", ["11050501"])).Error);
        Assert.Empty(await e.Mapeos.ListarAsync(c.Id, "PCCO"));
    }

    [Fact(DisplayName = "HU-11 CA5: si el guardado falla se informa cuántas asignaciones no se guardaron")]
    public async Task GuardadoFalla()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();
        var servicio = new ServicioMapeo(e.Clientes, e.Sociedades, new MapeoRoto(), e.Acceso, e.Sap);

        var r = await servicio.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501", "11100501", "11200501"]);

        Assert.Equal(0, r.Guardadas);
        Assert.Equal("No se pudo guardar el mapeo. La asignación de 3 cuentas no se guardó. Vuelve a asignarlas.", r.Error);
    }

    [Fact(DisplayName = "HU-11 CA4: si el ERP no responde se muestra el mensaje del sistema")]
    public async Task ErpFalla()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();
        e.Opciones.FallaCuentas = true;

        var v = (await e.ServicioMapeo.ObtenerAsync(c.Id, "1000", null, false))!;

        Assert.Equal(SimulatedSapGateway.MensajeFalloCuentas, v.Error);
        Assert.Empty(v.Cuentas);
    }

    [Fact(DisplayName = "HU-11 y RN-05: si el plan de la sociedad no coincide con el del ERP se pide corregirlo en Sociedades")]
    public async Task PlanDistinto()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();
        var guardadas = (await e.Sociedades.ListarAsync(c.Id)).ToList();
        guardadas[0].PlanCuentas = "PCXX";
        await e.Sociedades.GuardarAsync(c.Id, guardadas);

        var v = (await e.ServicioMapeo.ObtenerAsync(c.Id, "1000", null, false))!;

        Assert.Equal("El plan de cuentas de 1000 en el ERP es PCCO, no PCXX. Corrígelo en Sociedades.", v.Error);
    }

    [Fact(DisplayName = "HU-11: sin sociedades en SAP no hay cuentas que mapear")]
    public async Task SinSociedadesSap()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();

        var v = (await e.ServicioMapeo.ObtenerAsync(c.Id, null, null, false))!;

        Assert.Equal(ReglasMapeo.MensajeSinSociedades, v.Error);
        Assert.Null(v.Sociedad);
    }

    private sealed class MapeoRoto : IMapeoRepository
    {
        public Task<IReadOnlyList<AsignacionCuenta>> ListarAsync(Guid clienteId, string planCuentas, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AsignacionCuenta>>([]);

        public Task GuardarAsync(Guid clienteId, string planCuentas, IReadOnlyList<string> cuentas, string? rubro, CancellationToken ct = default) =>
            throw new Microsoft.Data.Sqlite.SqliteException("database is locked", 5);
    }
}
