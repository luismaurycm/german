using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;

namespace Acelerador.Core.Consolidacion;

/// <summary>
/// Motor de referencia de la consolidación: calcula lo que deben producir en BPC el paquete de importación y los scripts
/// que el acelerador despliega (RF-17, RF-19, RF-26, RF-33). En SAP real lo ejecuta BPC; aquí especifica el resultado
/// esperado y alimenta al simulador. Los importes siguen la convención de SAP: débitos positivos y créditos negativos.
/// </summary>
public static class MotorConsolidacion
{
    public const string Modelo = GeneradorModeloBpc.Modelo;
    public const string MonedaLocal = "LC";
    public const string FlujoCierre = "F_CLO";
    public const string FlujoConversion = "F_CTA";
    public const string OrigenCarga = "INPUT";
    public const string OrigenConversion = "CONVERSION";
    public const string OrigenEliminacionIc = "ELIM_IC";
    public const string IntcoNinguno = "I_NONE";

    /// <summary>Periodo en BPC (TIME): «2026-09» → «2026.09».</summary>
    public static string PeriodoBpc(string periodo) => periodo.Replace('-', '.');

    /// <summary>
    /// RF-17 (HU-27): registros en moneda local de una sociedad. La intercompañía solo es con sociedades del grupo (RN-07):
    /// un socio que no está en el grupo (cliente, proveedor) queda sin intercompañía.
    /// </summary>
    public static IReadOnlyList<RegistroBpc> Importar(string sociedad, string plan, string periodo, IEnumerable<SaldoErp> saldos, IReadOnlySet<string> grupo) =>
        saldos
            .GroupBy(s => (s.Cuenta, Intco: grupo.Contains(s.Socio) && s.Socio != sociedad ? $"I_{s.Socio}" : IntcoNinguno))
            .Select(g => new RegistroBpc(Modelo, sociedad, GeneradorModeloBpc.IdCuenta(plan, g.Key.Cuenta), g.Key.Intco, PeriodoBpc(periodo), MonedaLocal, FlujoCierre, OrigenCarga, g.Sum(s => s.Importe)))
            .Where(r => r.Importe != 0m)
            .ToList();

    /// <summary>
    /// RF-26 y RF-33 (HU-30, HU-31): convierte los registros en moneda local de una sociedad a una moneda de presentación con
    /// la tasa de cada clase de cuenta (RN-11) y registra la diferencia en conversión en la cuenta de patrimonio (RN-18),
    /// de modo que el balance convertido suma cero. Devuelve null y la tasa que falta si no hay tasa.
    /// </summary>
    /// <param name="tasa">Unidades de una moneda por dólar, por moneda y tipo de tasa; null si falta.</param>
    public static (IReadOnlyList<RegistroBpc> Registros, string? Falta) Convertir(
        IReadOnlyList<RegistroBpc> locales,
        string monedaEntidad,
        string monedaPresentacion,
        IReadOnlyDictionary<string, ClaseCuenta> clases,
        Func<string, string, decimal?> tasa)
    {
        var lista = new List<RegistroBpc>();
        foreach (var r in locales)
        {
            var clase = clases.GetValueOrDefault(r.Cuenta, ClaseCuenta.Activo);
            var tipo = ReglasRubros.TipoTasa(clase);
            decimal importe;
            if (monedaEntidad == monedaPresentacion)
            {
                importe = r.Importe;
            }
            else
            {
                var deEntidad = tasa(monedaEntidad, tipo);
                var dePresentacion = tasa(monedaPresentacion, tipo);
                if (deEntidad is null or 0m || dePresentacion is null)
                {
                    return ([], $"{(deEntidad is null or 0m ? monedaEntidad : monedaPresentacion)} ({tipo})");
                }

                importe = decimal.Round(r.Importe / deEntidad.Value * dePresentacion.Value, 2);
            }

            lista.Add(r with { Moneda = monedaPresentacion, Importe = importe });
        }

        // RN-18: lo que no cuadra por aplicar tasas distintas va al patrimonio, no a resultados.
        var balance = lista.Where(r => ReglasRubros.EsBalance(clases.GetValueOrDefault(r.Cuenta, ClaseCuenta.Activo))).Sum(r => r.Importe);
        if (balance != 0m && locales.Count > 0)
        {
            var e = locales[0];
            lista.Add(new RegistroBpc(Modelo, e.Entidad, ReglasRubros.CuentaDiferenciaConversion.Id, IntcoNinguno, e.Periodo, monedaPresentacion, FlujoConversion, OrigenConversion, -balance));
        }

        return (lista, null);
    }

    /// <summary>
    /// RF-19 (HU-29): elimina las partidas intercompañía entre sociedades que se integran (matriz o integración global). Cada
    /// lado se reversa contra la cuenta de diferencias intercompañía: si las partes concilian, esa cuenta queda en cero; si
    /// no, muestra la diferencia (C01). Las sociedades con método participación o fuera del perímetro no se eliminan (RN-13).
    /// </summary>
    public static IReadOnlyList<RegistroBpc> EliminarIntercompania(
        IEnumerable<RegistroBpc> convertidos,
        IReadOnlyDictionary<string, MetodoConsolidacion> metodos,
        string grupo)
    {
        bool Integra(string sociedad) => metodos.TryGetValue(sociedad, out var m) && m is MetodoConsolidacion.Matriz or MetodoConsolidacion.IntegracionGlobal;

        var lista = new List<RegistroBpc>();
        foreach (var r in convertidos.Where(r => r.Intco != IntcoNinguno && r.Origen == OrigenCarga))
        {
            var socio = r.Intco[2..];
            if (!Integra(r.Entidad) || !Integra(socio) || r.Cuenta == ReglasRubros.CuentaDiferenciaIntercompania.Id)
            {
                continue;
            }

            lista.Add(r with { Origen = OrigenEliminacionIc, Importe = -r.Importe, Grupo = grupo });
            lista.Add(r with { Cuenta = ReglasRubros.CuentaDiferenciaIntercompania.Id, Origen = OrigenEliminacionIc, Importe = r.Importe, Grupo = grupo });
        }

        return lista;
    }

    /// <summary>Suma por cuenta de los registros del grupo en una moneda: lo que reportaría BPC consolidado.</summary>
    public static IReadOnlyDictionary<string, decimal> Consolidado(IEnumerable<RegistroBpc> registros, IReadOnlySet<string> sociedades, string moneda) =>
        registros.Where(r => r.Moneda == moneda && sociedades.Contains(r.Entidad))
            .GroupBy(r => r.Cuenta)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Importe));
}
