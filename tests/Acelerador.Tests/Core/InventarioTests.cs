using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-07")]
public class ReglasInventarioTests
{
    [Fact(DisplayName = "HU-07 CA1: el inventario tiene cuatro pestañas en orden: extractores, InfoProviders, modelos BPC y campos Z")]
    public void CuatroPestanas()
    {
        Assert.Equal(
            [TipoInventario.Extractores, TipoInventario.InfoProviders, TipoInventario.ModelosBpc, TipoInventario.CamposZ],
            ReglasInventario.Pasos.Select(p => p.Tipo));
        Assert.Equal([SistemaSap.Erp, SistemaSap.Bw, SistemaSap.Bpc, SistemaSap.Erp], ReglasInventario.Pasos.Select(p => p.Sistema));
    }

    [Theory(DisplayName = "HU-07 CA1: la pestaña de InfoProviders dice InfoCubos o aDSO según la versión de BW")]
    [InlineData(ProductoBw.Bw75, "7.5", "InfoProviders (InfoCubos)")]
    [InlineData(ProductoBw.Bw4Hana, "2.0", "InfoProviders (aDSO)")]
    [InlineData(ProductoBw.NoReconocido, "7.4", "InfoProviders (InfoCubos o aDSO)")]
    public void PestanaSegunVersion(ProductoBw producto, string release, string esperado) =>
        Assert.Equal(esperado, ReglasInventario.Pestana(TipoInventario.InfoProviders, new VersionBw(producto, release)));

    [Fact(DisplayName = "HU-07 CA2: la bodega existe si algún InfoProvider es de la bodega financiera (RN-03)")]
    public void BodegaExistente()
    {
        var inventario = new Inventario { ClienteId = Guid.NewGuid() };
        inventario.Objetos[TipoInventario.InfoProviders] = [new("ZSD_C01", "Ventas", true, "InfoCubo")];
        Assert.False(inventario.BodegaExistente);
        Assert.Equal(ReglasInventario.AvisoSinBodega, ReglasInventario.AvisoBodega(inventario.BodegaExistente));

        inventario.Objetos[TipoInventario.InfoProviders] = [new("ZACC_C01", "Saldos", true, "InfoCubo", EsBodegaFinanciera: true)];
        Assert.True(inventario.BodegaExistente);
        Assert.Equal("Se usará la bodega existente; no se recrea.", ReglasInventario.AvisoBodega(true));
    }

    [Fact(DisplayName = "HU-07: cada pestaña vacía tiene su mensaje (estado vacío de UX-03)")]
    public void VacioPorPestana()
    {
        Assert.Equal("No se encontraron campos Z.", ReglasInventario.Vacio(TipoInventario.CamposZ).Titulo);
        Assert.Equal("El cliente no tiene bodega en BW.", ReglasInventario.Vacio(TipoInventario.InfoProviders).Titulo);
        Assert.Equal("Se generará completa en el plan de generación.", ReglasInventario.Vacio(TipoInventario.InfoProviders).Detalle);
        Assert.Equal("No hay modelos BPC.", ReglasInventario.Vacio(TipoInventario.ModelosBpc).Titulo);
    }

    [Fact(DisplayName = "RF-09: al volver a escanear se conservan los campos Z elegidos que siguen existiendo")]
    public void ConservaSeleccion()
    {
        ObjetoInventario[] campos = [new("ZZREGION", "Región", true, "BSEG"), new("ZZNUEVO", "Nuevo", true, "BSEG")];
        Assert.Equal(new HashSet<string> { "ZZREGION" }, ReglasInventario.ConservarSeleccion(["ZZREGION", "ZZBORRADO"], campos));
    }

    [Fact(DisplayName = "HU-07: solo se continúa a módulos con un escaneo completo")]
    public void BloqueoParaContinuar()
    {
        Assert.Equal(ReglasInventario.MensajeSinEscaneo, ReglasInventario.BloqueoParaContinuar(null));
        var inventario = new Inventario { ClienteId = Guid.NewGuid(), Fecha = DateTimeOffset.UtcNow };
        Assert.Null(ReglasInventario.BloqueoParaContinuar(inventario));
        inventario.Fallo = new FalloEscaneo(TipoInventario.ModelosBpc, "sin permiso", DateTimeOffset.UtcNow);
        Assert.Equal(ReglasInventario.MensajeEscaneoFallido, ReglasInventario.BloqueoParaContinuar(inventario));
    }
}

[Trait("HU", "HU-07")]
public class ServicioDiagnosticoTests
{
    private static async Task<(Entorno, Cliente)> ClienteEnDiagnosticoAsync(PerfilSimulado perfil = PerfilSimulado.ABw75)
    {
        var e = await Entorno.CrearAsync(perfil);
        var c = await e.ClienteAsync();
        await e.ConectarTodoAsync(c.Id);
        await e.ServicioConexiones.ValidarErpBwAsync(c.Id);
        Assert.Empty(await e.ServicioConexiones.ContinuarADiagnosticoAsync(c.Id));
        return (e, c);
    }

    [Fact(DisplayName = "HU-07 CA1: el escaneo lee las cuatro listas con el usuario técnico y solo lee (RN-19)")]
    public async Task EscaneaCuatroListas()
    {
        var (e, c) = await ClienteEnDiagnosticoAsync();
        await using var _ = e;

        var inv = await e.ServicioDiagnostico.EscanearAsync(c.Id);

        Assert.True(inv.Escaneado);
        Assert.Null(inv.Fallo);
        Assert.Contains(inv.Lista(TipoInventario.Extractores), o => o.NombreTecnico == "0FI_GL_14" && o.Activo);
        Assert.Contains(inv.Lista(TipoInventario.Extractores), o => o.NombreTecnico == "0FI_AA_11" && !o.Activo);
        Assert.All(inv.Lista(TipoInventario.InfoProviders), o => Assert.Equal("InfoCubo", o.Detalle));
        Assert.Single(inv.Lista(TipoInventario.ModelosBpc));
        Assert.Equal(["ZZREGION", "ZZLINEA", "ZZNOTAS"], inv.Lista(TipoInventario.CamposZ).Select(o => o.NombreTecnico));
        Assert.Equal(@"CM\consultor1", inv.Usuario);
        Assert.Equal(e.Reloj.Ahora, inv.Fecha);
        Assert.DoesNotContain(e.Sap.Llamadas, l => l.EsEscritura);
        Assert.Contains(e.Sap.Llamadas, l => l.Operacion == "Z_ACC_INVENTARIO ModelosBpc" && l.Sistema == SistemaSap.Bpc);
    }

    [Fact(DisplayName = "HU-07 CA1: en BW/4HANA los InfoProviders son aDSO")]
    public async Task Bw4HanaAdso()
    {
        var (e, c) = await ClienteEnDiagnosticoAsync(PerfilSimulado.ABw4Hana);
        await using var _ = e;

        var inv = await e.ServicioDiagnostico.EscanearAsync(c.Id);

        Assert.All(inv.Lista(TipoInventario.InfoProviders), o => Assert.Equal("aDSO", o.Detalle));
        Assert.Equal("0FI_ACDOCA_10", inv.Lista(TipoInventario.Extractores)[0].NombreTecnico);
        Assert.All(inv.Lista(TipoInventario.CamposZ), o => Assert.Equal("ACDOCA", o.Detalle));
    }

    [Fact(DisplayName = "HU-07 CA2: si BW ya tiene la bodega, se marca Bodega existente (RN-03)")]
    public async Task BodegaExistente()
    {
        var (e, c) = await ClienteEnDiagnosticoAsync();
        await using var _ = e;

        Assert.False((await e.ServicioDiagnostico.EscanearAsync(c.Id)).BodegaExistente);

        e.Opciones.BodegaExistente = true;
        var inv = await e.ServicioDiagnostico.EscanearAsync(c.Id);
        Assert.True(inv.BodegaExistente);
        Assert.True((await e.Inventarios.ObtenerAsync(c.Id))!.BodegaExistente);
    }

    [Fact(DisplayName = "HU-07 CA3: Volver a escanear repite el inventario y actualiza la fecha del último escaneo")]
    public async Task VolverAEscanear()
    {
        var (e, c) = await ClienteEnDiagnosticoAsync();
        await using var _ = e;
        var primero = await e.ServicioDiagnostico.EscanearAsync(c.Id);
        await e.ServicioDiagnostico.GuardarCamposZAsync(c.Id, ["ZZREGION"], continuar: false);

        e.Reloj.Ahora = e.Reloj.Ahora.AddHours(2);
        e.Opciones.CamposZAdicionales.Add("ZZNUEVO");
        var segundo = await e.ServicioDiagnostico.EscanearAsync(c.Id);

        Assert.True(segundo.Fecha > primero.Fecha);
        Assert.Equal(e.Reloj.Ahora, (await e.Inventarios.ObtenerAsync(c.Id))!.Fecha);
        Assert.Contains(segundo.Lista(TipoInventario.CamposZ), o => o.NombreTecnico == "ZZNUEVO");
        Assert.Equal(new HashSet<string> { "ZZREGION" }, segundo.CamposZIncorporados);
        Assert.Equal(8, (await e.Registro.ListarAsync(c.Id)).Count(x => x.Paso.StartsWith(ServicioDiagnostico.PrefijoPaso, StringComparison.Ordinal)));
    }

    [Fact(DisplayName = "HU-07 CA4: si el escaneo falla se guarda el paso que falló y el mensaje del sistema, y no se continúa")]
    public async Task FalloConPaso()
    {
        var (e, c) = await ClienteEnDiagnosticoAsync();
        await using var _ = e;
        e.Opciones.FallaInventario = TipoInventario.ModelosBpc;

        var inv = await e.ServicioDiagnostico.EscanearAsync(c.Id);

        Assert.False(inv.Escaneado);
        Assert.Equal(TipoInventario.ModelosBpc, inv.Fallo!.Tipo);
        Assert.Equal(SimulatedSapGateway.MensajeFalloInventario(TipoInventario.ModelosBpc), inv.Fallo.Mensaje);
        var reg = (await e.Registro.ListarAsync(c.Id)).First(x => x.Resultado == ResultadoEjecucion.Fallido);
        Assert.Equal("Inventario: Modelos BPC", reg.Paso);
        Assert.Equal("SAP BPC", reg.Sistema);
        Assert.DoesNotContain(e.Sap.Llamadas, l => l.Operacion == "Z_ACC_INVENTARIO CamposZ");

        var r = await e.ServicioDiagnostico.GuardarCamposZAsync(c.Id, [], continuar: true);
        Assert.False(r.Exitosa);
        Assert.Equal(Pasos.Diagnostico, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);
    }

    [Fact(DisplayName = "HU-07 CA4: un fallo después de un escaneo bueno conserva el inventario anterior y bloquea Continuar")]
    public async Task FalloConservaAnterior()
    {
        var (e, c) = await ClienteEnDiagnosticoAsync();
        await using var _ = e;
        await e.ServicioDiagnostico.EscanearAsync(c.Id);

        e.Opciones.FallaInventario = TipoInventario.Extractores;
        var inv = await e.ServicioDiagnostico.EscanearAsync(c.Id);

        Assert.True(inv.Escaneado);
        Assert.NotNull(inv.Fallo);
        Assert.Equal(4, inv.Lista(TipoInventario.Extractores).Count);
        Assert.Equal(ReglasInventario.MensajeEscaneoFallido, (await e.ServicioDiagnostico.GuardarCamposZAsync(c.Id, [], continuar: true)).Mensaje);
    }

    [Fact(DisplayName = "HU-07 CA4 y RNF-Seg-02: el mensaje de fallo no lleva la credencial")]
    public async Task FalloSinCredencial()
    {
        var (e, c) = await ClienteEnDiagnosticoAsync();
        await using var _ = e;
        var gateway = new GatewayQueEcoaLaClave(e.Sap);
        var servicio = new ServicioDiagnostico(e.Clientes, e.Inventarios, e.Acceso, e.Registro, gateway, e.Usuario, e.Reloj);

        var inv = await servicio.EscanearAsync(c.Id);

        Assert.DoesNotContain(Entorno.Clave, inv.Fallo!.Mensaje, StringComparison.Ordinal);
        Assert.All(await e.Registro.ListarAsync(c.Id), x => Assert.DoesNotContain(Entorno.Clave, x.Mensaje, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "RF-09: los campos Z elegidos se guardan; Continuar avanza a Módulos")]
    public async Task CamposZYContinuar()
    {
        var (e, c) = await ClienteEnDiagnosticoAsync();
        await using var _ = e;
        await e.ServicioDiagnostico.EscanearAsync(c.Id);

        var r = await e.ServicioDiagnostico.GuardarCamposZAsync(c.Id, ["ZZREGION", "ZZINVENTADO"], continuar: true);

        Assert.True(r.Exitosa);
        Assert.Equal(new HashSet<string> { "ZZREGION" }, (await e.Inventarios.ObtenerAsync(c.Id))!.CamposZIncorporados);
        Assert.Equal(Pasos.Modulos, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);
    }

    [Fact(DisplayName = "HU-07: sin conexión probada el escaneo falla en el primer paso con el motivo")]
    public async Task SinConexion()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();

        var inv = await e.ServicioDiagnostico.EscanearAsync(c.Id);

        Assert.Equal(TipoInventario.Extractores, inv.Fallo!.Tipo);
        Assert.Equal("Prueba primero con éxito la conexión a SAP ERP en Conexiones.", inv.Fallo.Mensaje);
        Assert.DoesNotContain(e.Sap.Llamadas, l => l.Operacion.StartsWith("Z_ACC_INVENTARIO", StringComparison.Ordinal));
    }

    /// <summary>Simula un sistema que devuelve la clave dentro del mensaje de error del inventario.</summary>
    private sealed class GatewayQueEcoaLaClave(ISapGateway interno) : ISapGateway
    {
        public Task<ResultadoPrueba> ProbarConexionAsync(DestinoSap destino, CancellationToken ct = default) => interno.ProbarConexionAsync(destino, ct);

        public Task<ResultadoValidacionErpBw> ValidarConexionErpBwAsync(DestinoSap erp, DestinoSap bw, CancellationToken ct = default) =>
            interno.ValidarConexionErpBwAsync(erp, bw, ct);

        public Task<ResultadoDeteccion> DetectarPaisajeAsync(DestinoSap erp, DestinoSap bw, DestinoSap bpc, CancellationToken ct = default) =>
            interno.DetectarPaisajeAsync(erp, bw, bpc, ct);

        public Task<ResultadoInventario> LeerInventarioAsync(DestinoSap destino, TipoInventario tipo, CancellationToken ct = default) =>
            Task.FromResult(new ResultadoInventario(false, $"logon failed for {destino.Parametros.UsuarioTecnico} with {destino.Credencial.Revelar()}", []));

        public Task<ResultadoSociedades> LeerSociedadesAsync(DestinoSap erp, CancellationToken ct = default) => interno.LeerSociedadesAsync(erp, ct);

        public Task<ResultadoCuentas> LeerCuentasAsync(DestinoSap erp, string sociedad, CancellationToken ct = default) => interno.LeerCuentasAsync(erp, sociedad, ct);

        public Task<ResultadoOperacionSap> EjecutarAsync(DestinoSap destino, OperacionSap operacion, CancellationToken ct = default) => interno.EjecutarAsync(destino, operacion, ct);
    }
}
