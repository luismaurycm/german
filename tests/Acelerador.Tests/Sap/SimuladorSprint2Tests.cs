using Acelerador.Sap;
using Acelerador.Sap.Nco;
using Acelerador.Sap.Simulador;

namespace Acelerador.Tests.Sap;

/// <summary>El simulador del Sprint 2: inventario, sociedades y cuentas, con sus convenciones de error.</summary>
public class SimuladorSprint2Tests
{
    private static DestinoSap Destino(SistemaSap s, string host = "bw-ejemplo.local") =>
        new(s, new ParametrosConexion(host, "100", "RFC_ACEL"), new CredencialSap("x"));

    [Fact(DisplayName = "HU-07: el simulador responde las convenciones de host bodega, sinpermiso y vacio")]
    [Trait("HU", "HU-07")]
    public async Task ConvencionesInventario()
    {
        var sap = new SimulatedSapGateway(new OpcionesSimulador());

        var bodega = await sap.LeerInventarioAsync(Destino(SistemaSap.Bw, "bw-bodega.local"), TipoInventario.InfoProviders);
        var sinPermiso = await sap.LeerInventarioAsync(Destino(SistemaSap.Bpc, "bw-sinpermiso.local"), TipoInventario.ModelosBpc);
        var vacio = await sap.LeerInventarioAsync(Destino(SistemaSap.Erp, "erp-vacio.local"), TipoInventario.CamposZ);

        Assert.Contains(bodega.Objetos, o => o.EsBodegaFinanciera);
        Assert.False(sinPermiso.Exitosa);
        Assert.True(vacio.Exitosa);
        Assert.Empty(vacio.Objetos);
        Assert.All(sap.Llamadas, l => Assert.False(l.EsEscritura));
    }

    [Fact(DisplayName = "HU-09 y HU-11: el simulador devuelve las sociedades del ERP y las cuentas del plan de cada una")]
    [Trait("HU", "HU-11")]
    public async Task SociedadesYCuentas()
    {
        var sap = new SimulatedSapGateway(new OpcionesSimulador());

        var sociedades = await sap.LeerSociedadesAsync(Destino(SistemaSap.Erp));
        var cuentas = await sap.LeerCuentasAsync(Destino(SistemaSap.Erp), "3000");
        var inexistente = await sap.LeerCuentasAsync(Destino(SistemaSap.Erp), "T901");

        Assert.Equal(["1000", "2000", "3000"], sociedades.Sociedades.Select(s => s.Codigo));
        Assert.Equal(("PCPA", "USD", SimulatedSapGateway.PeriodoSaldos), (cuentas.PlanCuentas, cuentas.Moneda, cuentas.Periodo));
        Assert.False(inexistente.Exitosa);
        Assert.Equal("La sociedad T901 no existe en el ERP.", inexistente.Mensaje);
    }

    [Fact(DisplayName = "HU-01: el conector real responde «pendiente de HU-01» también en inventario, sociedades y cuentas")]
    [Trait("HU", "HU-01")]
    public async Task NcoPendiente()
    {
        var nco = new NcoSapGateway();

        Assert.Equal(NcoSapGateway.MensajePendiente, (await nco.LeerInventarioAsync(Destino(SistemaSap.Bw), TipoInventario.Extractores)).Mensaje);
        Assert.Equal(NcoSapGateway.MensajePendiente, (await nco.LeerSociedadesAsync(Destino(SistemaSap.Erp))).Mensaje);
        Assert.Equal(NcoSapGateway.MensajePendiente, (await nco.LeerCuentasAsync(Destino(SistemaSap.Erp), "1000")).Mensaje);
    }
}
