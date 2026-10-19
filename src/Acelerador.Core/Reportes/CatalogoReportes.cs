using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reportes;

/// <summary>De dónde lee un reporte: consolidación y presupuesto de BPC (RN-08), activos fijos y deterioros de BW (RN-12).</summary>
public enum FuenteReporte
{
    Bpc,
    Bw,
}

/// <summary>Dónde se ve la vista previa de un reporte o plantilla.</summary>
public enum VistaReporte
{
    Ux11,
    Ux13,
    Ux15,
    Ux14,
}

/// <summary>
/// Un reporte o plantilla del catálogo de UX-10 (RF-20, RF-27, RF-29, RF-34): identificador, nombre, módulo, fuente, ola
/// de entrega y pantalla de vista previa. <see cref="Disponible"/> indica si esta versión ya lo genera.
/// </summary>
public sealed record DefinicionReporte(string Id, string Nombre, Modulo Modulo, FuenteReporte Fuente, string? Ola, VistaReporte Vista, bool Disponible)
{
    /// <summary>Nombre del archivo Excel: «R01_Estado_de_situacion_financiera.xlsx».</summary>
    public string Archivo => $"{Id}_{Formato.Archivo(Nombre)}.xlsx";

    /// <summary>Reportes con la estructura común de rubros de UX-11 (parámetros sociedad, filiales, moneda y periodo).</summary>
    public bool EsEstadoFinanciero => Id[0] == 'R';
}

/// <summary>Resultado de generar un reporte del catálogo (HU-48 CA4): generado, o con error y su causa.</summary>
public sealed record ReporteGenerado(Guid ClienteId, string Reporte, bool Exitoso, string? Mensaje, DateTimeOffset Fecha, string Usuario);

/// <summary>Los 34 reportes y plantillas del prototipo (UX-10): R01 a R20, P01 a P05, A01 a A03, D01 y D02, C01 a C04.</summary>
public static class CatalogoReportes
{
    private static DefinicionReporte R(string id, string nombre, string ola, bool disponible = false) =>
        new(id, nombre, Modulo.Consolidacion, FuenteReporte.Bpc, ola, VistaReporte.Ux11, disponible);

    public static readonly IReadOnlyList<DefinicionReporte> Todos =
    [
        R("R01", "Estado de situación financiera", "1", true),
        R("R02", "Estado de resultados integral", "2", true),
        R("R03", "Estado de cambios en el patrimonio", "2", true),
        R("R04", "Estado de flujos de efectivo", "2", true),
        R("R05", "Hoja de eliminaciones intercompañía", "1", true),
        R("R06", "Saldos y transacciones con partes relacionadas", "2", true),
        R("R07", "Efectivo y equivalentes de efectivo", "1", true),
        R("R08", "Cuentas por cobrar", "3"),
        R("R09", "Carteras colectivas", "3"),
        R("R10", "Propiedad, planta y equipo", "3"),
        R("R11", "Intangibles", "3"),
        R("R12", "Cuentas por pagar", "3"),
        R("R13", "Obligaciones financieras", "3"),
        R("R14", "Beneficios a empleados", "3"),
        R("R15", "Provisiones y contingencias", "3"),
        R("R16", "Impuesto a las ganancias", "3"),
        R("R17", "Dividendos pagados", "3"),
        R("R18", "Dividendos recibidos", "3"),
        R("R19", "Ingresos de actividades ordinarias", "3"),
        R("R20", "Informe a junta directiva", "3"),
        new("P01", "Presupuesto frente a ejecución", Modulo.Presupuesto, FuenteReporte.Bpc, null, VistaReporte.Ux11, false),
        new("P02", "Presupuesto de gastos por centro de costo", Modulo.Presupuesto, FuenteReporte.Bpc, null, VistaReporte.Ux11, false),
        new("P03", "Presupuesto de ingresos por centro de beneficio", Modulo.Presupuesto, FuenteReporte.Bpc, null, VistaReporte.Ux11, false),
        new("P04", "Ejecución acumulada y proyección de cierre", Modulo.Presupuesto, FuenteReporte.Bpc, null, VistaReporte.Ux11, false),
        new("P05", "Formulario de captura de presupuesto", Modulo.Presupuesto, FuenteReporte.Bpc, null, VistaReporte.Ux11, false),
        new("A01", "Movimiento de activos fijos por clase", Modulo.ActivosFijos, FuenteReporte.Bw, null, VistaReporte.Ux11, false),
        new("A02", "Depreciación del periodo", Modulo.ActivosFijos, FuenteReporte.Bw, null, VistaReporte.Ux11, false),
        new("A03", "Listado de activos fijos por sociedad", Modulo.ActivosFijos, FuenteReporte.Bw, null, VistaReporte.Ux11, false),
        new("D01", "Movimiento del deterioro", Modulo.Deterioros, FuenteReporte.Bw, null, VistaReporte.Ux11, false),
        new("D02", "Deterioro por cuenta y sociedad", Modulo.Deterioros, FuenteReporte.Bw, null, VistaReporte.Ux11, false),
        new("C01", "Conciliación intercompañía por pareja de sociedades", Modulo.Consolidacion, FuenteReporte.Bpc, "1", VistaReporte.Ux11, true),
        new("C02", "Participaciones y métodos (plantilla)", Modulo.Consolidacion, FuenteReporte.Bpc, "1", VistaReporte.Ux13, true),
        new("C03", "Validación de cuadre por sociedad", Modulo.Consolidacion, FuenteReporte.Bpc, "1", VistaReporte.Ux11, true),
        new("C04", "Estado del cierre por sociedad y paso", Modulo.Consolidacion, FuenteReporte.Bpc, "1", VistaReporte.Ux15, true),
    ];

    public static DefinicionReporte? Buscar(string? id) => Todos.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>RF-29: solo los reportes de los módulos que eligió el cliente (UX-04).</summary>
    public static IReadOnlyList<DefinicionReporte> DelCliente(Cliente cliente) => Todos.Where(r => cliente.Modulos.Contains(r.Modulo)).ToList();

    /// <summary>Reportes de consolidación de las olas 1 y 2: los que C04 exige generados para dar el cierre por completo.</summary>
    public static IReadOnlyList<DefinicionReporte> DelCierre => Todos.Where(r => r.Modulo == Modulo.Consolidacion && r.EsEstadoFinanciero && r.Ola is "1" or "2").ToList();
}

internal static class Formato
{
    /// <summary>«Estado de situación financiera» → «Estado_de_situacion_financiera».</summary>
    public static string Archivo(string nombre)
    {
        var sinTildes = new string(nombre.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
        return string.Join("_", sinTildes.Split([' ', ',', '(', ')'], StringSplitOptions.RemoveEmptyEntries));
    }
}
