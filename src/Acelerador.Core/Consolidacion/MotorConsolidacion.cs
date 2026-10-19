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
    public const string OrigenEliminacionInversion = "ELIM_INV";
    public const string OrigenParticipacion = "PARTICIPACION";
    public const string OrigenAjuste = "AJUSTE";
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
    /// <param name="excluir">Cuentas que no se eliminan como intercompañía: la inversión en subsidiarias la elimina la
    /// eliminación de inversión (HU-33), no la intercompañía.</param>
    public static IReadOnlyList<RegistroBpc> EliminarIntercompania(
        IEnumerable<RegistroBpc> convertidos,
        IReadOnlyDictionary<string, MetodoConsolidacion> metodos,
        string grupo,
        IReadOnlySet<string>? excluir = null)
    {
        bool Integra(string sociedad) => metodos.TryGetValue(sociedad, out var m) && m is MetodoConsolidacion.Matriz or MetodoConsolidacion.IntegracionGlobal;

        var lista = new List<RegistroBpc>();
        foreach (var r in convertidos.Where(r => r.Intco != IntcoNinguno && r.Origen == OrigenCarga))
        {
            var socio = r.Intco[2..];
            if (!Integra(r.Entidad) || !Integra(socio) || r.Cuenta == ReglasRubros.CuentaDiferenciaIntercompania.Id || excluir?.Contains(r.Cuenta) == true)
            {
                continue;
            }

            lista.Add(r with { Origen = OrigenEliminacionIc, Importe = -r.Importe, Grupo = grupo });
            lista.Add(r with { Cuenta = ReglasRubros.CuentaDiferenciaIntercompania.Id, Origen = OrigenEliminacionIc, Importe = r.Importe, Grupo = grupo });
        }

        return lista;
    }

    /// <summary>
    /// RF-32 (HU-33): para cada sociedad con integración global en el periodo, elimina la inversión de la matriz contra el
    /// 100 % del patrimonio de la subsidiaria, separa el interés minoritario (su % de patrimonio y de resultado) en
    /// <c>ZACC_IM</c> y deja la diferencia entre inversión y patrimonio eliminado en <c>ZACC_DIF_INV</c>.
    /// </summary>
    public static IReadOnlyList<RegistroBpc> EliminarInversion(
        IReadOnlyList<RegistroBpc> convertidos,
        IReadOnlyList<Participacion> participaciones,
        string matriz,
        IReadOnlySet<string> cuentasInversion,
        IReadOnlyDictionary<string, ClaseCuenta> clases,
        string grupo)
    {
        var lista = new List<RegistroBpc>();
        foreach (var moneda in convertidos.Select(r => r.Moneda).Distinct())
        {
            var enMoneda = convertidos.Where(r => r.Moneda == moneda && r.Grupo.Length == 0).ToList();
            foreach (var sub in participaciones.Where(p => p.Metodo == MetodoConsolidacion.IntegracionGlobal && p.Sociedad != matriz))
            {
                var s = sub.Sociedad;
                var deSub = enMoneda.Where(r => r.Entidad == s).ToList();
                if (deSub.Count == 0)
                {
                    continue;
                }

                var periodo = deSub[0].Periodo;
                var p = sub.Propiedad / 100m;
                var inversion = enMoneda.Where(r => r.Entidad == matriz && r.Intco == $"I_{s}" && cuentasInversion.Contains(r.Cuenta)).ToList();
                var patrimonio = deSub.Where(r => clases.GetValueOrDefault(r.Cuenta) == ClaseCuenta.Patrimonio).ToList();
                var resultado = deSub.Where(r => clases.GetValueOrDefault(r.Cuenta) == ClaseCuenta.ResultadoEjercicio).Sum(r => r.Importe);
                var totalPatrimonio = patrimonio.Sum(r => r.Importe);
                var totalInversion = inversion.Sum(r => r.Importe);
                RegistroBpc Nuevo(string entidad, string cuenta, string intco, decimal importe) =>
                    new(Modelo, entidad, cuenta, intco, periodo, moneda, FlujoCierre, OrigenEliminacionInversion, decimal.Round(importe, 2), grupo);

                lista.AddRange(inversion.Select(r => r with { Origen = OrigenEliminacionInversion, Importe = -r.Importe, Grupo = grupo }));
                lista.AddRange(patrimonio.Select(r => r with { Origen = OrigenEliminacionInversion, Importe = -r.Importe, Grupo = grupo }));
                lista.Add(Nuevo(s, ReglasRubros.CuentaInteresMinoritario.Id, IntcoNinguno, (1 - p) * (totalPatrimonio + resultado)));
                lista.Add(Nuevo(s, ReglasRubros.CuentaResultadoMinoritario.Id, IntcoNinguno, -(1 - p) * resultado));
                lista.Add(Nuevo(matriz, ReglasRubros.CuentaDiferenciaInversion.Id, $"I_{s}", totalInversion + p * totalPatrimonio));
            }
        }

        // Lo que deja el redondeo se ajusta en la diferencia de inversión para que la eliminación cuadre.
        return Cuadrar(lista, ReglasRubros.CuentaDiferenciaInversion.Id);
    }

    /// <summary>
    /// RN-13 (HU-34): las coligadas (método participación) no se integran línea a línea; la matriz reconoce su % del resultado
    /// de la coligada en la inversión y en «Participación en resultados de asociadas». Sus transacciones intercompañía no se
    /// eliminan y las sociedades fuera del perímetro no participan.
    /// </summary>
    public static IReadOnlyList<RegistroBpc> AplicarParticipacion(
        IReadOnlyList<RegistroBpc> convertidos,
        IReadOnlyList<Participacion> participaciones,
        string matriz,
        string cuentaInversion,
        IReadOnlyDictionary<string, ClaseCuenta> clases,
        string grupo)
    {
        var lista = new List<RegistroBpc>();
        foreach (var moneda in convertidos.Select(r => r.Moneda).Distinct())
        {
            foreach (var col in participaciones.Where(p => p.Metodo == MetodoConsolidacion.Participacion))
            {
                var deCol = convertidos.Where(r => r.Moneda == moneda && r.Grupo.Length == 0 && r.Entidad == col.Sociedad).ToList();
                if (deCol.Count == 0)
                {
                    continue;
                }

                var parte = decimal.Round(col.Propiedad / 100m * deCol.Where(r => clases.GetValueOrDefault(r.Cuenta) == ClaseCuenta.ResultadoEjercicio).Sum(r => r.Importe), 2);
                var periodo = deCol[0].Periodo;
                RegistroBpc Nuevo(string cuenta, string intco, decimal importe) =>
                    new(Modelo, matriz, cuenta, intco, periodo, moneda, FlujoCierre, OrigenParticipacion, importe, grupo);
                lista.Add(Nuevo(cuentaInversion, $"I_{col.Sociedad}", -parte));
                lista.Add(Nuevo(ReglasRubros.CuentaParticipacionResultado.Id, $"I_{col.Sociedad}", parte));
                lista.Add(Nuevo(ReglasRubros.CuentaParticipacionEjercicio.Id, IntcoNinguno, parte));
            }
        }

        return lista;
    }

    /// <summary>
    /// Consolidado del grupo en una moneda: los datos de las sociedades que se integran (matriz e integración global) más los
    /// ajustes del grupo (eliminaciones, participación). Coligadas y sociedades fuera del perímetro no se suman (RN-13).
    /// </summary>
    public static IReadOnlyDictionary<string, decimal> Consolidado(IEnumerable<RegistroBpc> registros, IReadOnlyList<Participacion> participaciones, string grupo, string moneda)
    {
        var integradas = participaciones.Where(p => p.Metodo is MetodoConsolidacion.Matriz or MetodoConsolidacion.IntegracionGlobal).Select(p => p.Sociedad).ToHashSet();
        return registros
            .Where(r => r.Modelo == Modelo && r.Moneda == moneda && ((r.Grupo.Length == 0 && integradas.Contains(r.Entidad)) || r.Grupo == grupo))
            .GroupBy(r => r.Cuenta)
            .ToDictionary(g => g.Key, g => g.Sum(r => r.Importe));
    }

    private static IReadOnlyList<RegistroBpc> Cuadrar(List<RegistroBpc> lista, string cuentaAjuste)
    {
        foreach (var g in lista.GroupBy(r => (r.Moneda, r.Periodo)).ToList())
        {
            var resto = g.Sum(r => r.Importe);
            if (resto != 0m)
            {
                var i = lista.FindLastIndex(r => r.Moneda == g.Key.Moneda && r.Periodo == g.Key.Periodo && r.Cuenta == cuentaAjuste);
                if (i >= 0)
                {
                    lista[i] = lista[i] with { Importe = lista[i].Importe - resto };
                }
            }
        }

        return lista;
    }
}
