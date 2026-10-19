namespace Acelerador.Sap.Nco;

/// <summary>
/// Conector real por RFC con SAP NCo 3.1 (ADR-03). <b>PENDIENTE DE LA PRUEBA TÉCNICA HU-01</b>
/// (riesgo R-02): todavía no referencia NCo ni abre conexiones. Cada método documenta las llamadas
/// que hará; hasta que la prueba confirme el mecanismo, devuelve un resultado fallido explicativo
/// en lugar de lanzar, para que la pantalla lo muestre como cualquier error de SAP.
/// </summary>
/// <remarks>
/// Guion de la prueba: docs/05-arquitectura/spike-HU-01.md. Firmas ABAP: abap/.
/// Al implementarlo:
/// <list type="number">
/// <item>Referenciar sapnco.dll y sapnco_utils.dll (NCo 3.1 para .NET Core/.NET 8, x64) desde lib/ y aceptar la licencia de SAP.</item>
/// <item>Crear un <c>IDestinationConfiguration</c> en memoria con ASHOST, SYSNR, CLIENT, USER, PASSWD (de ICredentialStore), LANG=ES y SNC_* si el cliente lo exige; nunca en archivos.</item>
/// <item>Traducir <c>RfcCommunicationException</c> y <c>RfcLogonException</c> a resultados fallidos con el mensaje de SAP.</item>
/// </list>
/// </remarks>
public sealed class NcoSapGateway : ISapGateway
{
    public const string MensajePendiente =
        "El conector RFC real (SAP NCo 3.1) está pendiente de la prueba técnica HU-01. Usa el simulador (Sap:Modo = Simulado).";

    /// <summary>RFC_PING (conectividad y logon) seguido de RFC_SYSTEM_INFO (SID, release).</summary>
    public Task<ResultadoPrueba> ProbarConexionAsync(DestinoSap destino, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoPrueba(false, MensajePendiente));

    /// <summary>Z_ACC_BW_VALIDAR_FUENTE en BW con el sistema lógico del ERP (RSA1: sistema fuente activo, prueba de RFC de retorno).</summary>
    public Task<ResultadoValidacionErpBw> ValidarConexionErpBwAsync(DestinoSap erp, DestinoSap bw, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoValidacionErpBw(false, MensajePendiente));

    /// <summary>
    /// Z_ACC_SISTEMA_INFO en ERP y BW (SID y componentes de CVERS) y Z_ACC_BPC_INFO en BPC (release y tipo de
    /// modelo de cada ambiente). El resultado de componentes se interpreta con <see cref="InterpretadorComponentes"/>.
    /// </summary>
    public Task<ResultadoDeteccion> DetectarPaisajeAsync(DestinoSap erp, DestinoSap bw, DestinoSap bpc, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoDeteccion(false, MensajePendiente));

    /// <summary>
    /// Z_ACC_INVENTARIO con IV_TIPO = EXTRACTORES (ROOSOURCE en el ERP), INFOPROVIDERS (RSDCUBE en BW 7.5 o RSOADSO en
    /// BW/4HANA, con la marca de bodega financiera), MODELOS_BPC (UJA_APPL, por confirmar) o CAMPOS_Z (DD03L sobre BSEG o
    /// ACDOCA). Devuelve JSON; si EV_TRUNCADO viene marcado se informa en el mensaje.
    /// </summary>
    public Task<ResultadoInventario> LeerInventarioAsync(DestinoSap destino, TipoInventario tipo, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoInventario(false, MensajePendiente, []));

    /// <summary>BAPI_COMPANYCODE_GETLIST y BAPI_COMPANYCODE_GETDETAIL (moneda y plan de cuentas de T001).</summary>
    public Task<ResultadoSociedades> LeerSociedadesAsync(DestinoSap erp, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoSociedades(false, MensajePendiente, []));

    /// <summary>
    /// BAPI_GL_ACC_GETLIST para el plan de la sociedad, nodo de la versión de estructura de balance (por confirmar en
    /// HU-01: lectura de FAGL_011ZC o un módulo Z) y BAPI_GL_GETGLACCPERIODBALANCES para el saldo del último periodo cerrado.
    /// </summary>
    public Task<ResultadoCuentas> LeerCuentasAsync(DestinoSap erp, string sociedad, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoCuentas(false, MensajePendiente, "", "", "", []));

    /// <summary>
    /// Llama la función Z de <see cref="OperacionSap.Funcion"/> (Z_ACC_CREAR_OBJETO, Z_ACC_ACTIVAR, Z_ACC_BPC_*) con la
    /// definición JSON y traduce ET_MENSAJES: EXISTE → <see cref="ResultadoEscritura.Existente"/>.
    /// </summary>
    public Task<ResultadoOperacionSap> EjecutarAsync(DestinoSap destino, OperacionSap operacion, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoOperacionSap(false, MensajePendiente));

    /// <summary>Z_ACC_BPC_LEER sobre el modelo (RSDRI / UJQ, por confirmar en HU-01) con los filtros de la consulta.</summary>
    public Task<ResultadoConsultaBpc> ConsultarBpcAsync(DestinoSap bpc, ConsultaBpc consulta, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoConsultaBpc(false, MensajePendiente, [], []));

    public Task<ResultadoConsultaBw> ConsultarBwAsync(DestinoSap bw, ConsultaBw consulta, CancellationToken ct = default) =>
        Task.FromResult(new ResultadoConsultaBw(false, MensajePendiente, []));
}
