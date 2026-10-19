using Acelerador.Core.Dominio;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>Reglas de HU-07: pasos del escaneo, pestañas según la versión, bodega existente (RN-03) y campos Z (RF-09).</summary>
public static class ReglasInventario
{
    public const string AvisoBodegaExistente = "Se usará la bodega existente; no se recrea.";
    public const string AvisoSinBodega = "El cliente no tiene bodega en BW: se generará completa.";
    public const string MensajeSinEscaneo = "Escanea el sistema del cliente antes de continuar.";
    public const string Alcance = "extractores activos, InfoProviders, modelos BPC y campos Z";
    public const string MensajeEscaneoFallido = "El último escaneo no terminó. Vuelve a escanear antes de continuar.";

    /// <summary>Orden del escaneo y sistema que responde cada paso.</summary>
    public static readonly IReadOnlyList<PasoInventario> Pasos =
    [
        new(TipoInventario.Extractores, SistemaSap.Erp, "Extractores activos"),
        new(TipoInventario.InfoProviders, SistemaSap.Bw, "InfoProviders"),
        new(TipoInventario.ModelosBpc, SistemaSap.Bpc, "Modelos BPC"),
        new(TipoInventario.CamposZ, SistemaSap.Erp, "Campos Z"),
    ];

    public static PasoInventario Paso(TipoInventario tipo) => Pasos.First(p => p.Tipo == tipo);

    /// <summary>BW 7.5 usa InfoCubos y BW/4HANA aDSO (HU-07 CA1).</summary>
    public static string TipoInfoProvider(VersionBw? bw) => bw?.Producto switch
    {
        ProductoBw.Bw4Hana => "aDSO",
        ProductoBw.Bw75 => "InfoCubos",
        _ => "InfoCubos o aDSO",
    };

    public static string Pestana(TipoInventario tipo, VersionBw? bw) => tipo switch
    {
        TipoInventario.InfoProviders => $"InfoProviders ({TipoInfoProvider(bw)})",
        _ => Paso(tipo).Nombre,
    };

    /// <summary>Mensaje de la pestaña sin objetos (estado vacío de UX-03).</summary>
    public static (string Titulo, string Detalle) Vacio(TipoInventario tipo) => tipo switch
    {
        TipoInventario.Extractores => ("No se encontraron extractores.", "Los extractores estándar se activan en el plan de generación."),
        TipoInventario.InfoProviders => ("El cliente no tiene bodega en BW.", "Se generará completa en el plan de generación."),
        TipoInventario.ModelosBpc => ("No hay modelos BPC.", "El modelo de consolidación se creará en el plan de generación."),
        _ => ("No se encontraron campos Z.", "El cliente no ha creado campos adicionales en SAP."),
    };

    public static string AvisoBodega(bool existe) => existe ? AvisoBodegaExistente : AvisoSinBodega;

    /// <summary>Al volver a escanear se conservan los campos Z elegidos que todavía existen.</summary>
    public static ISet<string> ConservarSeleccion(IEnumerable<string> previa, IEnumerable<ObjetoInventario> camposZ)
    {
        var existentes = camposZ.Select(c => c.NombreTecnico).ToHashSet(StringComparer.Ordinal);
        return previa.Where(existentes.Contains).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Por qué no se puede continuar a módulos, o null si se puede.</summary>
    public static string? BloqueoParaContinuar(Inventario? inventario) => inventario switch
    {
        null or { Escaneado: false } => MensajeSinEscaneo,
        { Fallo: not null } => MensajeEscaneoFallido,
        _ => null,
    };

    /// <summary>Resumen del escaneo para el registro de ejecuciones.</summary>
    public static string Resumen(Inventario inventario) =>
        string.Join(", ", Pasos.Select(p => $"{inventario.Lista(p.Tipo).Count} {p.Nombre.ToLowerInvariant()}"))
        + $"; bodega existente: {(inventario.BodegaExistente ? "sí" : "no")}.";
}
