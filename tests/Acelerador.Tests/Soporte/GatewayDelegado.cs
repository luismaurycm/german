using Acelerador.Sap;

namespace Acelerador.Tests.Soporte;

/// <summary>Decorador de <see cref="ISapGateway"/>: las pruebas sobrescriben solo la operación que quieren alterar.</summary>
public class GatewayDelegado(ISapGateway interno) : ISapGateway
{
    public virtual Task<ResultadoPrueba> ProbarConexionAsync(DestinoSap destino, CancellationToken ct = default) => interno.ProbarConexionAsync(destino, ct);

    public virtual Task<ResultadoValidacionErpBw> ValidarConexionErpBwAsync(DestinoSap erp, DestinoSap bw, CancellationToken ct = default) =>
        interno.ValidarConexionErpBwAsync(erp, bw, ct);

    public virtual Task<ResultadoDeteccion> DetectarPaisajeAsync(DestinoSap erp, DestinoSap bw, DestinoSap bpc, CancellationToken ct = default) =>
        interno.DetectarPaisajeAsync(erp, bw, bpc, ct);

    public virtual Task<ResultadoInventario> LeerInventarioAsync(DestinoSap destino, TipoInventario tipo, CancellationToken ct = default) =>
        interno.LeerInventarioAsync(destino, tipo, ct);

    public virtual Task<ResultadoSociedades> LeerSociedadesAsync(DestinoSap erp, CancellationToken ct = default) => interno.LeerSociedadesAsync(erp, ct);

    public virtual Task<ResultadoCuentas> LeerCuentasAsync(DestinoSap erp, string sociedad, CancellationToken ct = default) => interno.LeerCuentasAsync(erp, sociedad, ct);

    public virtual Task<ResultadoOperacionSap> EjecutarAsync(DestinoSap destino, OperacionSap operacion, CancellationToken ct = default) =>
        interno.EjecutarAsync(destino, operacion, ct);

    public virtual Task<ResultadoConsultaBpc> ConsultarBpcAsync(DestinoSap bpc, ConsultaBpc consulta, CancellationToken ct = default) =>
        interno.ConsultarBpcAsync(bpc, consulta, ct);

    public virtual Task<ResultadoConsultaBw> ConsultarBwAsync(DestinoSap bw, ConsultaBw consulta, CancellationToken ct = default) =>
        interno.ConsultarBwAsync(bw, consulta, ct);
}
