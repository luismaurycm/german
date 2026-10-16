namespace Acelerador.Sap;

/// <summary>
/// Única puerta del acelerador hacia SAP (arquitectura, sección 6). La implementación real usa
/// RFC con SAP NCo 3.1 contra el paquete Z (ADR-03, ADR-06); en pruebas y en desarrollo se usa
/// <see cref="Simulador.SimulatedSapGateway"/> para probar las reglas sin SAP (Regla 6).
/// </summary>
/// <remarks>
/// Los errores de SAP no se lanzan como excepción: vuelven en el resultado con el mensaje del
/// sistema, porque la pantalla los muestra tal cual (HU-03 CA2). El llamador sanitiza el mensaje
/// antes de mostrarlo o registrarlo (HU-04 CA4).
/// Todas las operaciones del Sprint 1 son de solo lectura: no crean objetos en el cliente.
/// </remarks>
public interface ISapGateway
{
    /// <summary>Abre una sesión RFC, hace RFC_PING y lee RFC_SYSTEM_INFO (HU-03).</summary>
    Task<ResultadoPrueba> ProbarConexionAsync(DestinoSap destino, CancellationToken ct = default);

    /// <summary>Verifica en BW que el sistema fuente del ERP está conectado y activo (HU-05, RN-01).</summary>
    Task<ResultadoValidacionErpBw> ValidarConexionErpBwAsync(DestinoSap erp, DestinoSap bw, CancellationToken ct = default);

    /// <summary>Lee SID, componentes de software y tipo de modelo BPC de los tres sistemas (HU-06).</summary>
    Task<ResultadoDeteccion> DetectarPaisajeAsync(DestinoSap erp, DestinoSap bw, DestinoSap bpc, CancellationToken ct = default);
}
