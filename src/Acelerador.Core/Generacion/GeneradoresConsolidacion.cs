using System.Text;
using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>Funciones Z de la lógica de BPC.</summary>
public static class FuncionesBpc
{
    public const string DesplegarScript = "Z_ACC_BPC_DESPLEGAR_SCRIPT";
    public const string Reglas = "Z_ACC_BPC_REGLAS";
    public const string CrearPaquete = "Z_ACC_BPC_CREAR_PAQUETE";
    public const string EjecutarPaquete = "Z_ACC_BPC_EJECUTAR_PAQUETE";
    public const string EjecutarLogica = "Z_ACC_BPC_EJECUTAR_LOGICA";
    public const string ValidarCuadre = "Z_ACC_VALIDAR_CUADRE";
}

/// <summary>
/// HU-28: scripts de lógica (UJFS) y reglas de negocio de BPC modelo estándar (RN-02), completados con los códigos del
/// cliente sin edición manual. Van en el paso del modelo BPC, después del respaldo UJBR (ADR-06, CA4).
/// </summary>
public sealed class GeneradorScripts : IGeneradorObjetos
{
    public const string Grupo = "Scripts de lógica";
    public const string ScriptConversion = "CONVERSION.LGF";
    public const string ScriptEliminacion = "ELIMINACION_IC.LGF";

    public bool Aplica(ContextoPlan c) => c.Cliente.Modulos.Contains(Modulo.Consolidacion) && c.Variante.Modelo == ModeloBpc.Estandar;

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var nota = $"Variante para modelo estándar ({c.Variante.Id}). RN-02";
        var grupo = ReglasCierre.Grupo(c.Sociedades);
        var monedas = ReglasCierre.MonedasPresentacion(c.Sociedades);
        yield return Objeto("Reglas de negocio", "CONVERSION_TASAS", "Reglas de conversión: cierre, promedio e histórica por tipo de cuenta (RN-11)", FuncionesBpc.Reglas, "REGLAS",
            new { tipo = "CONVERSION", reglas = Enum.GetValues<ClaseCuenta>().Select(k => new { clase = k.ToString(), tasa = ReglasRubros.TipoTasa(k) }), cuentaDiferencia = ReglasRubros.CuentaDiferenciaConversion.Id, flujo = MotorConsolidacion.FlujoConversion },
            $"La diferencia en conversión va a {ReglasRubros.CuentaDiferenciaConversion.Id} (patrimonio, {ReglasRubros.CuentaDiferenciaConversion.Rubro}). RN-18");
        yield return Objeto("Reglas de negocio", "ELIMINACION_IC", "Reglas de eliminación intercompañía", FuncionesBpc.Reglas, "REGLAS",
            new { tipo = "ELIMINACION", cuentaDiferencia = ReglasRubros.CuentaDiferenciaIntercompania.Id, origen = MotorConsolidacion.OrigenEliminacionIc });
        yield return Objeto("Script", ScriptConversion, $"Conversión a {string.Join(" y ", monedas)} de {string.Join(", ", c.Sociedades.Select(s => s.Codigo))}", FuncionesBpc.DesplegarScript, "SCRIPT",
            new { archivo = ScriptConversion, contenido = ScriptDeConversion(c, monedas) }, nota);
        yield return Objeto("Script", ScriptEliminacion, $"Eliminación intercompañía del grupo {grupo}", FuncionesBpc.DesplegarScript, "SCRIPT",
            new { archivo = ScriptEliminacion, contenido = ScriptDeEliminacion(c, grupo) }, nota);
    }

    /// <summary>Encabezado común: variante y fecha, para saber qué se desplegó.</summary>
    public static string Encabezado(ContextoPlan c, string archivo) =>
        $"// Acelerador de consolidación · CM Consulting · {archivo}\n// Variante para modelo estándar: {c.Variante.Id} ({c.Variante.Nombre}). RN-02\n// Generado con los datos del cliente {c.Cliente.Nombre}; no editar a mano.\n";

    public static string ScriptDeConversion(ContextoPlan c, IReadOnlyList<string> monedas)
    {
        var sb = new StringBuilder(Encabezado(c, ScriptConversion));
        sb.Append("*RUN_PROGRAM CURR_CONVERSION\n");
        sb.Append("  CATEGORY = %CATEGORY_SET%\n");
        sb.Append($"  CURRENCY = {string.Join(",", monedas)}\n");
        sb.Append("  TID_RA = %TIME_SET%\n");
        sb.Append($"  OTHER = [ENTITY={string.Join(",", c.Sociedades.Select(s => s.Codigo))}]\n");
        sb.Append($"  // Diferencia en conversión en {ReglasRubros.CuentaDiferenciaConversion.Id}, flujo {MotorConsolidacion.FlujoConversion} (RN-18)\n");
        sb.Append("*ENDRUN_PROGRAM\n");
        return sb.ToString();
    }

    public static string ScriptDeEliminacion(ContextoPlan c, string grupo)
    {
        var integradas = c.Sociedades.Where(s => s.Metodo is MetodoConsolidacion.Matriz or MetodoConsolidacion.IntegracionGlobal).Select(s => s.Codigo);
        var sb = new StringBuilder(Encabezado(c, ScriptEliminacion));
        sb.Append("*RUN_PROGRAM ICELIM\n");
        sb.Append("  CATEGORY = %CATEGORY_SET%\n");
        sb.Append($"  GROUP = {grupo}\n");
        sb.Append("  TID_RA = %TIME_SET%\n");
        sb.Append($"  OTHER = [ENTITY={string.Join(",", integradas)}]\n");
        sb.Append($"  // Contrapartida: {ReglasRubros.CuentaDiferenciaIntercompania.Id}. Cuentas intercompañía: {string.Join(",", CuentasIntercompania(c))}\n");
        sb.Append("*ENDRUN_PROGRAM\n");
        return sb.ToString();
    }

    /// <summary>Cuentas del cliente que pueden tener intercompañía: las mapeadas a cuentas por cobrar, por pagar, ingresos y gastos.</summary>
    public static IEnumerable<string> CuentasIntercompania(ContextoPlan c) =>
        c.Asignaciones.Where(a => a.Rubro is "F-00-102" or "F-00-201" || ReglasRubros.EsResultados(ReglasRubros.Clase(a.Rubro)))
            .Select(a => GeneradorModeloBpc.IdCuenta(a.PlanCuentas, a.Cuenta));

    private static ObjetoPlan Objeto(string tipo, string nombre, string descripcion, string funcion, string tipoObjeto, object definicion, string? nota = null) =>
        new(PasosGeneracion.ModeloBpc, Capa.Bpc, Grupo, SistemaSap.Bpc, tipo, nombre, descripcion, EstadoObjeto.Nuevo,
            new OperacionSap(funcion, tipoObjeto, nombre, Definicion.Json(definicion)), nota);
}

/// <summary>
/// HU-36, HU-27, HU-30, HU-31 y HU-29: validación de cuadre, importación a BPC, conversión y consolidación del periodo
/// inicial. Cada objeto es una ejecución de lógica en BPC o BW con sus parámetros.
/// </summary>
public sealed class GeneradorConsolidacion : IGeneradorObjetos
{
    public bool Aplica(ContextoPlan c) => c.Cliente.Modulos.Contains(Modulo.Consolidacion);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var periodo = ReglasCierre.PeriodoInicial(c.Fecha);
        var grupo = ReglasCierre.Grupo(c.Sociedades);
        var codigos = c.Sociedades.Select(s => s.Codigo).ToList();
        var monedas = ReglasCierre.MonedasPresentacion(c.Sociedades);
        var participantes = ReglasCierre.Participantes(c.Sociedades);

        // HU-36: cada sociedad (SAP y no SAP) se valida antes de importar (RN-16).
        foreach (var s in c.Sociedades)
        {
            var plan = s.EnSap ? s.PlanCuentas : ServicioNoSap.PlanDeCuentas(s, c.Sociedades);
            var mapeo = c.Asignaciones.Where(a => a.PlanCuentas == plan).ToDictionary(a => a.Cuenta, a => a.Rubro);
            yield return Objeto(PasosGeneracion.Cuadre, "Validación de cuadre", SistemaSap.Bw, "Validación", $"Cuadre {s.Codigo} {periodo}",
                $"Activo = pasivo + patrimonio, utilidad = resultado del ejercicio y cuentas mapeadas de {s.Codigo} en {periodo}", FuncionesBpc.ValidarCuadre, "VALIDACION",
                new { logica = "VALIDAR_CUADRE", sociedad = s.Codigo, s.EnSap, periodo, plan, mapeo, tolerancia = ReglaCuadre.ToleranciaProvisional },
                $"Tolerancia de redondeo {ReglaCuadre.Formato(ReglaCuadre.ToleranciaProvisional)} (provisional, pregunta 14 del DAF). RN-16");
        }

        // HU-27: solo en arquitectura A con BPC estándar (RN-19): paquete de Data Manager por sociedad y periodo.
        if (c.Cliente.Deteccion?.Arquitectura == Arquitectura.A && c.Variante.Modelo == ModeloBpc.Estandar)
        {
            yield return Objeto(PasosGeneracion.Importacion, "Importación a BPC", SistemaSap.Bpc, "Paquete de Data Manager", "ZACC_IMPORTAR",
                "Importación desde la bodega de BW al modelo de consolidación (INT-05)", FuncionesBpc.CrearPaquete, "PAQUETE",
                new { paquete = "ZACC_IMPORTAR", modelo = MotorConsolidacion.Modelo, origen = c.Variante.InfoProvider == TipoInfoProvider.Adso ? GeneradorBodegaBw4Hana.AdsoSaldos : GeneradorBodegaBw75.CuboSaldos, validarCuadre = true });
            foreach (var s in c.Sociedades)
            {
                var plan = s.EnSap ? s.PlanCuentas : ServicioNoSap.PlanDeCuentas(s, c.Sociedades);
                yield return Objeto(PasosGeneracion.Importacion, "Importación a BPC", SistemaSap.Bpc, "Importación", $"Importar {s.Codigo} {periodo}",
                    $"Ejecutar ZACC_IMPORTAR para {s.Codigo} en {periodo}", FuncionesBpc.EjecutarPaquete, "IMPORTACION",
                    new { logica = "IMPORTAR", paquete = "ZACC_IMPORTAR", sociedad = s.Codigo, s.EnSap, periodo, plan, grupo = codigos },
                    "Solo se importa si pasó la validación de cuadre (HU-36).");
            }
        }

        // HU-30 y HU-31: conversión a las dos monedas de presentación con la diferencia en conversión en patrimonio.
        yield return Objeto(PasosGeneracion.Conversion, "Conversión", SistemaSap.Bpc, "Ejecución de lógica", $"Conversión {periodo}",
            $"Convertir a {string.Join(" y ", monedas)} con cierre, promedio e histórica (RN-11)", FuncionesBpc.EjecutarLogica, "LOGICA",
            new
            {
                logica = "CONVERSION",
                script = GeneradorScripts.ScriptConversion,
                periodo,
                monedas = c.Sociedades.ToDictionary(s => s.Codigo, s => s.MonedaLocal),
                presentacion = monedas,
                clases = ReglasRubros.ClasesPorMiembro(c.Asignaciones).ToDictionary(k => k.Key, k => k.Value.ToString()),
            },
            $"Diferencia en conversión en {ReglasRubros.CuentaDiferenciaConversion.Id} (patrimonio, {ReglasRubros.CuentaDiferenciaConversion.Rubro}), no en resultados. RN-18");

        // HU-29: eliminación intercompañía entre las sociedades que se integran (RN-07, RN-13).
        yield return Objeto(PasosGeneracion.Consolidacion, "Consolidación", SistemaSap.Bpc, "Ejecución de lógica", $"Eliminación intercompañía {grupo} {periodo}",
            $"Eliminar las transacciones entre {string.Join(", ", participantes.Select(s => s.Codigo))} en {string.Join(" y ", monedas)}", FuncionesBpc.EjecutarLogica, "LOGICA",
            new
            {
                logica = "ELIMINACION_IC",
                script = GeneradorScripts.ScriptEliminacion,
                periodo,
                grupo,
                metodos = c.Sociedades.ToDictionary(s => s.Codigo, s => s.Metodo.ToString()),
                presentacion = monedas,
            },
            "Solo participan las sociedades del grupo. RN-07 · Resultado en R05 Hoja de eliminaciones intercompañía.");
    }

    private static ObjetoPlan Objeto(string paso, string grupo, SistemaSap sistema, string tipo, string nombre, string descripcion, string funcion, string tipoObjeto, object definicion, string? nota = null) =>
        new(paso, Capa.Bpc, grupo, sistema, tipo, nombre, descripcion, EstadoObjeto.Nuevo,
            new OperacionSap(funcion, tipoObjeto, nombre, Definicion.Json(definicion)), nota);
}
