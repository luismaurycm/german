using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>
/// HU-32: modelo de propiedad en BPC (RF-31) con la estructura inicial de HU-10 como primer periodo, y la plantilla C02
/// conectada a él, que el Usuario financiero usa cada mes (UX-13).
/// </summary>
public sealed class GeneradorPropiedad : IGeneradorObjetos
{
    public const string Grupo = "Modelo de propiedad";
    public const string Plantilla = "C02_Participaciones.xlsx";
    public const string Conexion = "ZACC_PROPIEDAD";
    public const string UsuarioInicial = "Acelerador (estructura inicial)";

    public bool Aplica(ContextoPlan c) => c.Cliente.Modulos.Contains(Modulo.Consolidacion);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var periodo = ReglasCierre.PeriodoInicial(c.Fecha);
        var grupo = ReglasCierre.Grupo(c.Sociedades);
        yield return Objeto("Dimensión", "ACCOUNTOWN", "Cuentas del modelo de propiedad", "Z_ACC_BPC_CREAR_DIMENSION", "DIMENSION",
            new { ambiente = GeneradorModeloBpc.Ambiente, dimension = "ACCOUNTOWN", tipo = "A" });
        yield return Objeto("Miembros", "ACCOUNTOWN", "% control, % consolidación, % propiedad y método", FuncionesZ.BpcCrearMiembro, "MIEMBROS",
            new
            {
                ambiente = GeneradorModeloBpc.Ambiente,
                dimension = "ACCOUNTOWN",
                miembros = new[]
                {
                    new MiembroBpc(ModeloPropiedad.Control, "% control", null), new MiembroBpc(ModeloPropiedad.Consolidacion, "% consolidación", null),
                    new MiembroBpc(ModeloPropiedad.Propiedad, "% propiedad", null), new MiembroBpc(ModeloPropiedad.Metodo, "Método de consolidación", null),
                },
            });
        yield return Objeto("Modelo", ModeloPropiedad.Modelo, "Modelo de propiedad (tipo Propiedad) por grupo, sociedad y periodo", "Z_ACC_BPC_CREAR_MODELO", "MODELO",
            new { ambiente = GeneradorModeloBpc.Ambiente, modelo = ModeloPropiedad.Modelo, tipo = "PROPIEDAD", dimensiones = new[] { "ACCOUNTOWN", "ENTITY", "INTCO", "TIME", "CATEGORY", "GROUP" } },
            "Se usa por periodo: un cambio aplica desde el periodo en que se registra. RN-14");
        yield return Objeto("Participaciones", $"Estructura inicial {periodo}", $"Estructura de HU-10 como primer periodo ({periodo}) del grupo {grupo}", "Z_ACC_BPC_GUARDAR_PARTICIPACIONES", "PARTICIPACIONES",
            new
            {
                logica = "PARTICIPACIONES",
                grupo,
                periodo,
                usuario = UsuarioInicial,
                fecha = c.Fecha,
                filas = EstructuraInicial(c.Sociedades).Select(p => new { sociedad = p.Sociedad, control = p.Control, consolidacion = p.Consolidacion, propiedad = p.Propiedad, metodo = p.Metodo.ToString() }),
            });
        yield return Objeto("Plantilla EPM", Plantilla, "C02 Participaciones y métodos, conectada al modelo de propiedad (UX-13)", "Z_ACC_BPC_SUBIR_PLANTILLA", "PLANTILLA",
            new
            {
                plantilla = "C02",
                archivo = Plantilla,
                carpeta = "PLANTILLAS",
                parametros = ParametrosC02(c.Sociedades, periodo),
            });
    }

    /// <summary>Parámetros de la plantilla C02 (conexión EPM, grupo, periodo y sociedades).</summary>
    public static object ParametrosC02(IReadOnlyList<Sociedad> sociedades, string periodo) => new
    {
        conexion = Conexion,
        grupo = ReglasCierre.Grupo(sociedades),
        periodo = MotorConsolidacion.PeriodoBpc(periodo),
        sociedades = sociedades.Select(s => new { codigo = s.Codigo, nombre = s.Nombre }),
    };

    /// <summary>
    /// HU-10 CA4: la estructura inicial como participaciones del primer periodo. La matriz y las de integración global se
    /// consolidan al 100 %; la coligada, en su % de propiedad; fuera del perímetro, 0.
    /// </summary>
    public static IReadOnlyList<Participacion> EstructuraInicial(IEnumerable<Sociedad> sociedades) =>
        ReglasSociedades.EstructuraInicial(sociedades)
            .Select(e => new Participacion(
                e.Sociedad,
                e.Metodo == MetodoConsolidacion.Matriz ? 100m : e.Porcentaje,
                e.Metodo switch { MetodoConsolidacion.Matriz or MetodoConsolidacion.IntegracionGlobal => 100m, MetodoConsolidacion.Participacion => e.Porcentaje, _ => 0m },
                e.Metodo == MetodoConsolidacion.Matriz ? 100m : e.Porcentaje,
                e.Metodo))
            .ToList();

    private static ObjetoPlan Objeto(string tipo, string nombre, string descripcion, string funcion, string tipoObjeto, object definicion, string? nota = null) =>
        new(PasosGeneracion.ModeloBpc, Capa.Bpc, Grupo, SistemaSap.Bpc, tipo, nombre, descripcion, EstadoObjeto.Nuevo,
            new OperacionSap(funcion, tipoObjeto, nombre, Definicion.Json(definicion)), nota);
}
