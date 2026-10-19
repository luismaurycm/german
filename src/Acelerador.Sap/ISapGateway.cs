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
/// Las operaciones de lectura no crean nada en el cliente; las escrituras van todas por <see cref="EjecutarAsync"/>.
/// </remarks>
public interface ISapGateway
{
    /// <summary>Abre una sesión RFC, hace RFC_PING y lee RFC_SYSTEM_INFO (HU-03).</summary>
    Task<ResultadoPrueba> ProbarConexionAsync(DestinoSap destino, CancellationToken ct = default);

    /// <summary>Verifica en BW que el sistema fuente del ERP está conectado y activo (HU-05, RN-01).</summary>
    Task<ResultadoValidacionErpBw> ValidarConexionErpBwAsync(DestinoSap erp, DestinoSap bw, CancellationToken ct = default);

    /// <summary>Lee SID, componentes de software y tipo de modelo BPC de los tres sistemas (HU-06).</summary>
    Task<ResultadoDeteccion> DetectarPaisajeAsync(DestinoSap erp, DestinoSap bw, DestinoSap bpc, CancellationToken ct = default);

    /// <summary>
    /// Lee un tipo de objeto del inventario (HU-07). Extractores y campos Z se leen del ERP; InfoProviders de BW;
    /// modelos de BPC del sistema BPC. El llamador elige el destino según <see cref="TipoInventario"/>.
    /// </summary>
    Task<ResultadoInventario> LeerInventarioAsync(DestinoSap destino, TipoInventario tipo, CancellationToken ct = default);

    /// <summary>Sociedades del ERP con moneda local y plan de cuentas (HU-09, RN-07: solo las del grupo).</summary>
    Task<ResultadoSociedades> LeerSociedadesAsync(DestinoSap erp, CancellationToken ct = default);

    /// <summary>Cuentas del plan de cuentas de una sociedad, con nodo y saldo (HU-11, RN-05, RN-10).</summary>
    Task<ResultadoCuentas> LeerCuentasAsync(DestinoSap erp, string sociedad, CancellationToken ct = default);

    /// <summary>Crea, activa o modifica un objeto en el cliente por el paquete Z (HU-14 en adelante). Idempotente (RN-03).</summary>
    Task<ResultadoOperacionSap> EjecutarAsync(DestinoSap destino, OperacionSap operacion, CancellationToken ct = default);

    /// <summary>Lee datos y validaciones del modelo BPC (vistas previas de reportes y controles C01 a C04).</summary>
    Task<ResultadoConsultaBpc> ConsultarBpcAsync(DestinoSap bpc, ConsultaBpc consulta, CancellationToken ct = default);

    /// <summary>Lee un InfoProvider de BW (reportes de activos fijos y deterioros, que no tienen modelo en BPC, RN-12).</summary>
    Task<ResultadoConsultaBw> ConsultarBwAsync(DestinoSap bw, ConsultaBw consulta, CancellationToken ct = default);
}
