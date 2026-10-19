using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>
/// HU-37 a HU-41: lo que el Usuario financiero usa cada mes para cerrar. Los estados de trabajo de BPC (bloqueo por sociedad
/// y periodo, RN-17), la configuración de asientos de ajuste (RN-15) y las plantillas EPM C01, C03, C04 y Asientos.
/// </summary>
public sealed class GeneradorCierre : IGeneradorObjetos
{
    public const string Grupo = "Controles del cierre";
    public const string EstadosTrabajo = "ZACC_ESTADOS";
    public const string Asientos = "ZACC_ASIENTOS";
    public const string PrefijoAsiento = "AJ-";
    public const string Conexion = "ZACC_CONSOLIDACION";

    /// <summary>Plantillas del cierre: identificador, archivo y descripción (las muestra UX-08 y luego el catálogo de UX-10).</summary>
    public static readonly IReadOnlyList<(string Id, string Archivo, string Descripcion)> Plantillas =
    [
        ("C01", "C01_Conciliacion_intercompania.xlsx", "C01 Conciliación intercompañía por pareja de sociedades (UX-11)"),
        ("C03", "C03_Validacion_cuadre.xlsx", "C03 Validación de cuadre por sociedad (UX-11)"),
        ("C04", "C04_Estado_del_cierre.xlsx", "C04 Estado del cierre con bloqueo por sociedad (UX-15)"),
        ("AJ", "Asientos_de_ajuste.xlsx", "Asientos de ajuste de consolidación (UX-14)"),
    ];

    public bool Aplica(ContextoPlan c) => c.Cliente.Modulos.Contains(Modulo.Consolidacion);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var grupo = ReglasCierre.Grupo(c.Sociedades);
        var periodo = ReglasCierre.PeriodoInicial(c.Fecha);
        yield return Objeto("Estados de trabajo", EstadosTrabajo, "Abierta o Bloqueada por sociedad y periodo en el modelo de consolidación", FuncionesBpc.ConfigurarEstados, "ESTADOS",
            new
            {
                ambiente = GeneradorModeloBpc.Ambiente,
                modelo = MotorConsolidacion.Modelo,
                dimensiones = new[] { "ENTITY", "TIME" },
                estados = new[] { new { codigo = "ABIERTA", bloquea = Array.Empty<string>() }, new { codigo = "BLOQUEADA", bloquea = new[] { "cargas", "asientos", "reprocesos" } } },
            },
            "Bloquear una sociedad no bloquea a las demás; no admite cargas, asientos ni reprocesos del periodo. RN-17");
        yield return Objeto("Asientos de ajuste", Asientos, $"Asientos {PrefijoAsiento}nnnn con sociedad, cuenta, intercompañía, débito, crédito y observación", FuncionesBpc.ConfigurarAsientos, "ASIENTOS",
            new
            {
                ambiente = GeneradorModeloBpc.Ambiente,
                modelo = MotorConsolidacion.Modelo,
                dimensiones = new[] { "ENTITY", "ACCOUNT", "INTCO", "FLOW", "AUDITTRAIL" },
                origen = MotorConsolidacion.OrigenAjuste,
                prefijo = PrefijoAsiento,
                requiereCuadre = true,
                respetaEstadosTrabajo = true,
            },
            "Solo se guardan asientos con débitos iguales a créditos. RN-15");
        foreach (var (id, archivo, descripcion) in Plantillas)
        {
            yield return Objeto("Plantilla EPM", archivo, descripcion, FuncionesBpc.SubirPlantilla, "PLANTILLA",
                new
                {
                    plantilla = id,
                    archivo,
                    carpeta = "PLANTILLAS",
                    parametros = new
                    {
                        conexion = Conexion,
                        grupo,
                        periodo = MotorConsolidacion.PeriodoBpc(periodo),
                        monedas = ReglasCierre.MonedasPresentacion(c.Sociedades),
                        umbral = ReglasConciliacion.UmbralProvisional,
                        sociedades = c.Sociedades.Select(s => new { codigo = s.Codigo, nombre = s.Nombre }),
                    },
                });
        }
    }

    private static ObjetoPlan Objeto(string tipo, string nombre, string descripcion, string funcion, string tipoObjeto, object definicion, string? nota = null) =>
        new(PasosGeneracion.ModeloBpc, Capa.Bpc, Grupo, SistemaSap.Bpc, tipo, nombre, descripcion, EstadoObjeto.Nuevo,
            new OperacionSap(funcion, tipoObjeto, nombre, Definicion.Json(definicion)), nota);
}
