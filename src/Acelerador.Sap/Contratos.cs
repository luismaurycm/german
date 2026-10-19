namespace Acelerador.Sap;

/// <summary>Sistemas SAP del cliente con los que habla el acelerador (RF-01).</summary>
public enum SistemaSap
{
    Erp,
    Bw,
    Bpc,
}

/// <summary>Parámetros de conexión RFC. No incluye la credencial: esa vive en el almacén de credenciales (ADR-04).</summary>
public sealed record ParametrosConexion(string Host, string Mandante, string UsuarioTecnico);

/// <summary>
/// Credencial del usuario técnico. Envuelve el secreto para que nunca salga por ToString,
/// interpolación o serialización accidental a un registro (RNF-Seg-02).
/// </summary>
public sealed class CredencialSap
{
    public const string Mascara = "••••••••";

    private readonly string _valor;

    public CredencialSap(string valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            throw new ArgumentException("La credencial no puede estar vacía.", nameof(valor));
        }

        _valor = valor;
    }

    /// <summary>Solo para el adaptador RFC y el sanitizador de mensajes.</summary>
    public string Revelar() => _valor;

    public override string ToString() => Mascara;
}

/// <summary>Destino RFC completo: sistema, parámetros y credencial.</summary>
public sealed record DestinoSap(SistemaSap Sistema, ParametrosConexion Parametros, CredencialSap Credencial);

/// <summary>Identificación del sistema que responde: SID y producto (p. ej. «SAP BW 7.5»).</summary>
public sealed record InfoSistemaSap(string Sid, string Producto);

public sealed record ResultadoPrueba(bool Exitosa, string Mensaje, InfoSistemaSap? Sistema = null);

public sealed record ResultadoValidacionErpBw(bool Exitosa, string Mensaje);

public enum ProductoBw
{
    NoReconocido,
    Bw75,
    Bw4Hana,
}

public enum ProductoBpc
{
    NoReconocido,
    Bpc101,
    Bpc2021,
}

public enum ModeloBpc
{
    Estandar,
    Embebido,
}

/// <summary>Versión de BW detectada. <see cref="Release"/> es el texto que devuelve SAP (p. ej. «7.5», «2.0», «7.4»).</summary>
public sealed record VersionBw(ProductoBw Producto, string Release);

public sealed record VersionBpc(ProductoBpc Producto, string Release, ModeloBpc Modelo);

/// <summary>Lo que el escáner lee de los tres sistemas para identificar la arquitectura (RF-04).</summary>
public sealed record PaisajeSap(string SidErp, string SidBw, string SidBpc, VersionBw Bw, VersionBpc Bpc);

public sealed record ResultadoDeteccion(bool Exitosa, string Mensaje, PaisajeSap? Paisaje = null);

/// <summary>Tipos de objeto que lee el escáner de inventario (RF-03, HU-07), en el orden del escaneo.</summary>
public enum TipoInventario
{
    /// <summary>Fuentes de datos (extractores) del ERP, activas o no.</summary>
    Extractores,

    /// <summary>InfoCubos (BW 7.5) o aDSO (BW/4HANA) de BW.</summary>
    InfoProviders,

    /// <summary>Ambientes y modelos de BPC.</summary>
    ModelosBpc,

    /// <summary>Campos Z/ZZ agregados por el cliente en la contabilidad (BSEG o ACDOCA), para RF-09.</summary>
    CamposZ,
}

/// <summary>
/// Un objeto que ya existe en el cliente. Solo metadatos: el escáner nunca lee datos financieros ni de terceros.
/// <see cref="Detalle"/> es el tipo de InfoProvider (InfoCubo o aDSO), el ambiente del modelo BPC o la tabla del campo Z.
/// <see cref="EsBodegaFinanciera"/> marca los InfoProviders de la bodega financiera (RN-03).
/// <see cref="TipoDato"/> es el tipo ABAP del campo Z (p. ej. «CHAR 4», «STRG»), para decidir si se puede incorporar (HU-17).
/// </summary>
public sealed record ObjetoInventario(
    string NombreTecnico,
    string Descripcion,
    bool Activo,
    string? Detalle = null,
    bool EsBodegaFinanciera = false,
    string? TipoDato = null);

public sealed record ResultadoInventario(bool Exitosa, string Mensaje, IReadOnlyList<ObjetoInventario> Objetos);

/// <summary>Sociedad (código de compañía) definida en el ERP, con su moneda local y su plan de cuentas (RF-06).</summary>
public sealed record SociedadErp(string Codigo, string Nombre, string Moneda, string PlanCuentas);

public sealed record ResultadoSociedades(bool Exitosa, string Mensaje, IReadOnlyList<SociedadErp> Sociedades);

/// <summary>
/// Cuenta de mayor del plan de cuentas de una sociedad, con el nodo de la jerarquía y el saldo acumulado de la
/// sociedad al cierre del último periodo, en su moneda local. El saldo solo alimenta la vista previa de RN-10.
/// </summary>
public sealed record CuentaErp(string Numero, string Descripcion, string Nodo, decimal Saldo);

/// <summary>Cuentas del plan de una sociedad. <see cref="Periodo"/> es el periodo de los saldos (aaaa-mm).</summary>
public sealed record ResultadoCuentas(
    bool Exitosa,
    string Mensaje,
    string PlanCuentas,
    string Moneda,
    string Periodo,
    IReadOnlyList<CuentaErp> Cuentas);

/// <summary>Qué hizo el paquete Z con un objeto: lo creó, ya existía (no lo toca, RN-03) o lo modificó.</summary>
public enum ResultadoEscritura
{
    Creado,
    Existente,
    Modificado,
}

/// <summary>
/// Una escritura en el cliente por el paquete Z (INT-02, INT-03): función RFC, tipo y nombre técnico del objeto y su
/// definición en JSON. El paquete Z es idempotente: si el objeto existe responde EXISTE y no lo modifica (RN-03).
/// </summary>
public sealed record OperacionSap(string Funcion, string TipoObjeto, string NombreTecnico, string Definicion);

/// <summary>
/// Resultado de una escritura. <see cref="ErrorDeComunicacion"/> distingue la falla de red o el tiempo agotado (se reintenta
/// una vez) de un error de negocio de SAP (no se reintenta), como pide la arquitectura (sección 8.3). En las cargas de
/// datos, <see cref="Cantidad"/> es cuántos registros cargó y <see cref="Aviso"/> lo que no cargó sin que el paso falle.
/// <see cref="Clave"/> es el identificador que asigna SAP, como el número de un asiento.
/// </summary>
public sealed record ResultadoOperacionSap(
    bool Exitosa,
    string Mensaje,
    ResultadoEscritura? Resultado = null,
    bool ErrorDeComunicacion = false,
    int? Cantidad = null,
    string? Aviso = null,
    string? Clave = null);

/// <summary>
/// Un registro del modelo BPC: sociedad, cuenta, intercompañía, periodo, moneda de presentación (LC, USD o la moneda
/// local del grupo), flujo, origen (carga, eliminaciones, conversión…) e importe. Grupo vacío = dato de la sociedad.
/// </summary>
public sealed record RegistroBpc(
    string Modelo,
    string Entidad,
    string Cuenta,
    string Intco,
    string Periodo,
    string Moneda,
    string Flujo,
    string Origen,
    decimal Importe,
    string Grupo = "");

/// <summary>Resultado de la validación de cuadre de una sociedad y periodo antes de importar (RN-16, HU-36, C03).</summary>
public sealed record ValidacionCuadre(
    string Sociedad,
    string Periodo,
    bool Importable,
    decimal DiferenciaBalance,
    decimal DiferenciaResultado,
    IReadOnlyList<string> CuentasSinMapear,
    bool Importada = false);

/// <summary>Consulta al modelo BPC (lo que harían EPM o los reportes C01 a C04). Null en un filtro = todos.</summary>
public sealed record ConsultaBpc(string Modelo, string? Periodo = null, string? Moneda = null, string? Origen = null, string? Entidad = null);

/// <summary>Un cambio auditado en BPC (RNF-Aud-02): quién, cuándo, qué dato, valor anterior y nuevo.</summary>
public sealed record AuditoriaBpc(DateTimeOffset Fecha, string Usuario, string Modelo, string Sociedad, string Periodo, string Dato, string Anterior, string Nuevo);

/// <summary>Una línea de un asiento de ajuste de consolidación (RF-35, UX-14).</summary>
public sealed record LineaAsiento(string Sociedad, string Cuenta, string Intco, decimal Debito, decimal Credito, string Observacion);

/// <summary>Un asiento de ajuste contabilizado en BPC, con número, usuario y fecha (RNF-Aud-02).</summary>
public sealed record AsientoBpc(
    string Numero,
    string Grupo,
    string Periodo,
    string Moneda,
    string Descripcion,
    string Usuario,
    DateTimeOffset Fecha,
    IReadOnlyList<LineaAsiento> Lineas,
    string Estado = "Contabilizado")
{
    public decimal Valor => Lineas.Sum(l => l.Debito);
}

/// <summary>Estado de trabajo de una sociedad en un periodo (RN-17): bloqueada o abierta, quién y cuándo lo cambió.</summary>
public sealed record BloqueoBpc(string Sociedad, string Periodo, bool Bloqueada, string Usuario, DateTimeOffset Fecha);

public sealed record ResultadoConsultaBpc(
    bool Exitosa,
    string Mensaje,
    IReadOnlyList<RegistroBpc> Registros,
    IReadOnlyList<ValidacionCuadre> Validaciones,
    IReadOnlyList<AuditoriaBpc>? Auditoria = null,
    IReadOnlyList<AsientoBpc>? Asientos = null,
    IReadOnlyList<BloqueoBpc>? Bloqueos = null);
