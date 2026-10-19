namespace Acelerador.Sap.Simulador;

/// <summary>Paisajes SAP que el simulador sabe imitar.</summary>
public enum PerfilSimulado
{
    /// <summary>Arquitectura A: ECC 6.0, BW 7.5 y BPC 10.1 modelo estándar en sistemas separados.</summary>
    ABw75,

    /// <summary>Arquitectura A: S/4HANA, BW/4HANA 2.0 y BPC 2021 modelo estándar (RNF-Comp-05).</summary>
    ABw4Hana,

    /// <summary>Arquitectura A con versiones sin variante (BW 7.4, BPC 10.0): «Versión no soportada aún».</summary>
    AVersionNoSoportada,

    /// <summary>Arquitectura A con BPC modelo embebido: sin variante en la versión 1 (D-04).</summary>
    ABpcEmbebido,

    /// <summary>Arquitectura B: S/4HANA con BW y BPC embebidos en el mismo sistema (RN-19).</summary>
    B,
}

/// <summary>
/// Configuración del simulador (sección «Simulador» de appsettings o variables Simulador__*).
/// Además de estas opciones, el simulador reconoce convenciones en los datos que escribe el
/// consultor, para poder recorrer los estados de error desde la pantalla:
/// <list type="bullet">
/// <item>host que contiene «falla» → la conexión falla (sistema no alcanzable);</item>
/// <item>credencial «invalida» → la conexión falla por usuario o clave incorrectos;</item>
/// <item>host de BW que contiene «sinfuente» → la validación ERP–BW falla;</item>
/// <item>host de BW que contiene «bodega» → BW ya tiene la bodega financiera (RN-03);</item>
/// <item>host de BPC que contiene «sinpermiso» → el escaneo falla al leer los modelos BPC;</item>
/// <item>host del ERP que contiene «vacio» → inventario sin objetos (estado vacío de UX-03).</item>
/// </list>
/// </summary>
public sealed class OpcionesSimulador
{
    public const string Seccion = "Simulador";

    public PerfilSimulado Perfil { get; set; } = PerfilSimulado.ABw75;

    /// <summary>Sistemas cuya prueba de conexión falla siempre.</summary>
    public List<SistemaSap> SistemasConFallo { get; set; } = [];

    public bool FallaValidacionErpBw { get; set; }

    public bool FallaDeteccion { get; set; }

    /// <summary>Latencia artificial por llamada, para ver el estado de carga en pantalla.</summary>
    public int LatenciaMs { get; set; }

    /// <summary>
    /// Solo pruebas: imita un sistema que devuelve la clave dentro del mensaje de error, para
    /// comprobar que el acelerador la oculta antes de mostrarla o registrarla (HU-04 CA4).
    /// </summary>
    public bool EcoDeCredencialEnErrores { get; set; }

    /// <summary>BW ya tiene la bodega financiera (RN-03).</summary>
    public bool BodegaExistente { get; set; }

    /// <summary>El cliente no tiene extractores, InfoProviders, modelos BPC ni campos Z.</summary>
    public bool InventarioVacio { get; set; }

    /// <summary>Tipo de objeto cuya lectura falla en el escaneo (falta de autorización).</summary>
    public TipoInventario? FallaInventario { get; set; }

    /// <summary>Campos Z que el cliente agregó después del primer escaneo (p. ej. ZZNUEVO).</summary>
    public List<string> CamposZAdicionales { get; set; } = [];

    /// <summary>La lectura de sociedades del ERP falla.</summary>
    public bool FallaSociedades { get; set; }

    /// <summary>La lectura de cuentas del ERP falla.</summary>
    public bool FallaCuentas { get; set; }

    /// <summary>Cuentas creadas en el ERP además de las del paisaje de ejemplo.</summary>
    public List<CuentaSimulada> CuentasAdicionales { get; set; } = [];
}

/// <summary>Cuenta adicional del simulador: plan de cuentas, número, descripción, nodo y saldo.</summary>
public sealed class CuentaSimulada
{
    public string Plan { get; set; } = "";

    public string Numero { get; set; } = "";

    public string Descripcion { get; set; } = "";

    public string Nodo { get; set; } = "";

    public decimal Saldo { get; set; }
}
