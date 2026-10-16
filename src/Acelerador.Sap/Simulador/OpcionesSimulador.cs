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
/// <item>host de BW que contiene «sinfuente» → la validación ERP–BW falla.</item>
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
}
