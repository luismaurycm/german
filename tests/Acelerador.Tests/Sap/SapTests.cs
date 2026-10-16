using Acelerador.Sap;
using Acelerador.Sap.Nco;
using Acelerador.Sap.Simulador;

namespace Acelerador.Tests.Sap;

public class SimuladorTests
{
    private static DestinoSap Destino(SistemaSap s, string host = "h.local", string clave = "x") =>
        new(s, new ParametrosConexion(host, "100", "RFC_ACEL"), new CredencialSap(clave));

    [Theory(DisplayName = "Simulador: imita los perfiles de arquitectura A y B")]
    [InlineData(PerfilSimulado.ABw75, "SAP BW 7.5", "BWP")]
    [InlineData(PerfilSimulado.ABw4Hana, "SAP BW/4HANA 2.0", "B4P")]
    [InlineData(PerfilSimulado.B, "BW embebido en S/4HANA", "S4H")]
    public async Task Perfiles(PerfilSimulado perfil, string producto, string sid)
    {
        var sap = new SimulatedSapGateway(new OpcionesSimulador { Perfil = perfil });
        var r = await sap.ProbarConexionAsync(Destino(SistemaSap.Bw));
        Assert.True(r.Exitosa);
        Assert.Equal(new InfoSistemaSap(sid, producto), r.Sistema);
    }

    [Fact(DisplayName = "Simulador: falla por configuración y por convención de host o credencial")]
    public async Task Fallos()
    {
        var sap = new SimulatedSapGateway(new OpcionesSimulador { SistemasConFallo = [SistemaSap.Bpc] });
        Assert.False((await sap.ProbarConexionAsync(Destino(SistemaSap.Bpc))).Exitosa);
        Assert.False((await sap.ProbarConexionAsync(Destino(SistemaSap.Erp, host: "erp-FALLA.local"))).Exitosa);
        Assert.Equal(SimulatedSapGateway.MensajeClaveIncorrecta, (await sap.ProbarConexionAsync(Destino(SistemaSap.Erp, clave: "invalida"))).Mensaje);
        Assert.True((await sap.ProbarConexionAsync(Destino(SistemaSap.Erp))).Exitosa);
    }

    [Fact(DisplayName = "Simulador: todas las operaciones del Sprint 1 son de solo lectura")]
    public async Task SoloLectura()
    {
        var sap = new SimulatedSapGateway(new OpcionesSimulador());
        await sap.ProbarConexionAsync(Destino(SistemaSap.Erp));
        await sap.ValidarConexionErpBwAsync(Destino(SistemaSap.Erp), Destino(SistemaSap.Bw));
        await sap.DetectarPaisajeAsync(Destino(SistemaSap.Erp), Destino(SistemaSap.Bw), Destino(SistemaSap.Bpc));
        Assert.Equal(5, sap.Llamadas.Count);
        Assert.DoesNotContain(sap.Llamadas, l => l.EsEscritura);
    }
}

[Trait("HU", "HU-01")]
public class ConectorNcoTests
{
    [Fact(DisplayName = "HU-01: el conector NCo está pendiente de la prueba técnica y lo informa sin lanzar excepciones")]
    public async Task Pendiente()
    {
        var nco = new NcoSapGateway();
        var d = new DestinoSap(SistemaSap.Bw, new ParametrosConexion("h", "100", "U"), new CredencialSap("x"));

        var prueba = await nco.ProbarConexionAsync(d);
        var validacion = await nco.ValidarConexionErpBwAsync(d, d);
        var deteccion = await nco.DetectarPaisajeAsync(d, d, d);

        Assert.False(prueba.Exitosa);
        Assert.False(validacion.Exitosa);
        Assert.False(deteccion.Exitosa);
        Assert.Contains("HU-01", prueba.Mensaje, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "HU-01: la versión de BW se interpreta desde los componentes de CVERS")]
    [InlineData("SAP_BW", "750", ProductoBw.Bw75, "7.5")]
    [InlineData("SAP_BW", "740", ProductoBw.NoReconocido, "7.4")]
    [InlineData("DW4CORE", "200", ProductoBw.Bw4Hana, "2.0")]
    [InlineData("DW4CORE", "400", ProductoBw.Bw4Hana, "2023")]
    [InlineData("SAP_BASIS", "750", ProductoBw.NoReconocido, "sin componente BW")]
    public void VersionBw(string componente, string release, ProductoBw producto, string texto)
    {
        var v = InterpretadorComponentes.InterpretarBw([new ComponenteSap("SAP_BASIS", "750"), new ComponenteSap(componente, release)]);
        Assert.Equal(new VersionBw(producto, texto), v);
    }

    [Fact(DisplayName = "HU-01: BPC 10.1 (CPMBPC 810) se reconoce; otro release queda no reconocido hasta confirmarlo")]
    public void VersionBpc()
    {
        Assert.Equal(new VersionBpc(ProductoBpc.Bpc101, "10.1", ModeloBpc.Estandar),
            InterpretadorComponentes.InterpretarBpc([new ComponenteSap("CPMBPC", "810")], ModeloBpc.Estandar));
        Assert.Equal(ProductoBpc.NoReconocido,
            InterpretadorComponentes.InterpretarBpc([new ComponenteSap("CPMBPC", "800")], ModeloBpc.Estandar).Producto);
        Assert.Equal("sin componente BPC",
            InterpretadorComponentes.InterpretarBpc([new ComponenteSap("SAP_BW", "750")], ModeloBpc.Estandar).Release);
    }
}
