using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Reportes;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>
/// HU-49 (RF-23): el presupuesto y la ejecución del ERP en BW desde 0FI_GL_14 y un modelo de planeación en BPC separado del
/// de consolidación, con las versiones de HU-12, los centros de costo y de beneficio, la carga inicial y las plantillas P01 a P05.
/// Solo se genera con Presupuesto marcado en UX-04.
/// </summary>
public sealed class GeneradorPresupuesto() : GeneradorModuloBw(
    Modulo.Presupuesto, PasosGeneracion.Presupuesto, "Presupuesto en BW", "PR",
    [("0FI_GL_14", "Partidas de contabilidad general con presupuesto y ejecución")],
    "Presupuesto y ejecución por cuenta, centro y mes",
    "Alimenta el modelo de planeación de BPC. RF-23")
{
    public const string Grupo = "Modelo de presupuesto";
    public const string Conexion = "ZACC_PLANEACION";

    public override IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        foreach (var o in base.Generar(c))
        {
            yield return o;
        }

        var anio = int.Parse(ReglasCierre.PeriodoInicial(c.Fecha)[..4], System.Globalization.CultureInfo.InvariantCulture);
        var versiones = c.Cliente.Versiones.Count == 0 ? VersionPresupuesto.Propuestas : [.. c.Cliente.Versiones];
        var anual = versiones.FirstOrDefault(v => v.Anual) ?? versiones[0];
        var enSap = c.Sociedades.Where(s => s.EnSap).ToList();
        yield return Objeto("Dimensión", "CATEGORY", "Versiones del presupuesto y la ejecución (ACTUAL)", "Z_ACC_BPC_CREAR_DIMENSION", "DIMENSION",
            new { ambiente = GeneradorModeloBpc.Ambiente, dimension = "CATEGORY", tipo = "C" });
        yield return Objeto("Miembros", "CATEGORY", $"ACTUAL y {string.Join(", ", versiones.Select(v => v.Codigo))}", FuncionesZ.BpcCrearMiembro, "MIEMBROS",
            new
            {
                ambiente = GeneradorModeloBpc.Ambiente,
                dimension = "CATEGORY",
                miembros = versiones.Select(v => new MiembroBpc(v.Codigo, v.Nombre, null)).Prepend(new MiembroBpc(MotorPresupuesto.Real, "Ejecución", null)),
            },
            "Versiones definidas en UX-04. RF-39");
        yield return Objeto("Dimensión", "COSTCENTER", "Centros de costo y de beneficio", "Z_ACC_BPC_CREAR_DIMENSION", "DIMENSION",
            new { ambiente = GeneradorModeloBpc.Ambiente, dimension = "COSTCENTER", tipo = "U" });
        yield return Objeto("Miembros", "COSTCENTER", "Centros de costo y de beneficio cargados desde BW (0COSTCENTER, 0PROFIT_CTR)", GeneradorDatosMaestros.Funcion, "CARGA",
            new { tipo = "MIEMBROS_BW", dimension = "COSTCENTER", infoObjects = new[] { "0COSTCENTER", "0PROFIT_CTR" }, sociedades = enSap.Select(s => s.Codigo) });
        yield return Objeto("Modelo", MotorPresupuesto.Modelo, "Modelo de planeación por cuenta, sociedad, versión, centro y mes", "Z_ACC_BPC_CREAR_MODELO", "MODELO",
            new { ambiente = GeneradorModeloBpc.Ambiente, modelo = MotorPresupuesto.Modelo, tipo = "PLANEACION", dimensiones = new[] { "ACCOUNT", "ENTITY", "CATEGORY", "COSTCENTER", "TIME", "RPTCURRENCY" } },
            "Independiente del modelo de consolidación: planear no cambia la consolidación. RF-23");
        yield return Objeto("Carga de datos", $"Presupuesto y ejecución {anio}", $"Presupuesto del ERP en {anual.Nombre} y ejecución (ACTUAL) de {anio}", FuncionesBpc.EjecutarPaquete, "PRESUPUESTO",
            new
            {
                logica = "PRESUPUESTO_CARGA",
                paquete = "ZACC_PRESUPUESTO",
                origen = InfoProvider(c),
                anio,
                versionAnual = anual.Codigo,
                sociedades = enSap.Select(s => new { codigo = s.Codigo, plan = s.PlanCuentas }),
            });
        foreach (var r in CatalogoReportes.Todos.Where(r => r.Modulo == Modulo.Presupuesto))
        {
            yield return Objeto("Plantilla EPM", r.Archivo, $"{r.Id} {r.Nombre}", FuncionesBpc.SubirPlantilla, "PLANTILLA",
                new { plantilla = "R", archivo = r.Archivo, carpeta = "PLANTILLAS", parametros = Parametros(r, c.Sociedades, versiones, anio) });
        }
    }

    /// <summary>Parámetros del Excel de un reporte de presupuesto: sociedad, versión y año (HU-51 CA3: la versión va en el encabezado).</summary>
    public static object Parametros(DefinicionReporte r, IReadOnlyList<Sociedad> sociedades, IReadOnlyList<VersionPresupuesto> versiones, int anio) => new
    {
        id = r.Id,
        nombre = r.Nombre,
        conexion = Conexion,
        periodo = anio.ToString(System.Globalization.CultureInfo.InvariantCulture),
        monedas = Array.Empty<string>(),
        sociedades = sociedades.Select(s => new { codigo = s.Codigo, nombre = s.Nombre }),
        parametros = new object[]
        {
            new { etiqueta = "Sociedad", valor = sociedades.FirstOrDefault()?.Codigo ?? "", opciones = sociedades.Select(s => s.Codigo) },
            new { etiqueta = "Versión", valor = versiones.FirstOrDefault(v => v.Anual)?.Codigo ?? versiones[0].Codigo, opciones = versiones.Select(v => v.Codigo) },
            new { etiqueta = "Año", valor = anio.ToString(System.Globalization.CultureInfo.InvariantCulture), opciones = Array.Empty<string>() },
        },
        encabezados = r.Id == "P05" ? new[] { "Cuenta", "Centro" } : new[] { "Rubro", "Concepto" },
        filas = Array.Empty<object>(),
    };

    private static ObjetoPlan Objeto(string tipo, string nombre, string descripcion, string funcion, string tipoObjeto, object definicion, string? nota = null) =>
        new(PasosGeneracion.Presupuesto, Capa.Bpc, Grupo, SistemaSap.Bpc, tipo, nombre, descripcion, EstadoObjeto.Nuevo,
            new OperacionSap(funcion, tipoObjeto, nombre, Definicion.Json(definicion)), nota);
}
