using Acelerador.Sap;
using Acelerador.Sap.Simulador;

namespace Acelerador.Core.Reglas;

/// <summary>
/// RN-16 (HU-36): una sociedad se importa a BPC solo si en el periodo el activo es igual al pasivo más el patrimonio, la
/// utilidad del estado de resultados es igual al resultado del ejercicio del balance y todas sus cuentas con saldo están
/// mapeadas. La tolerancia de redondeo es provisional (pregunta 14 del DAF): una diferencia igual a la tolerancia pasa.
/// </summary>
public static class ReglaCuadre
{
    public const decimal ToleranciaProvisional = 1.00m;

    /// <param name="rubroPorCuenta">Rubro de cada cuenta mapeada del plan de la sociedad.</param>
    public static ValidacionCuadre Validar(string sociedad, string periodo, IEnumerable<SaldoErp> saldos, IReadOnlyDictionary<string, string> rubroPorCuenta, decimal tolerancia)
    {
        var porCuenta = saldos.GroupBy(s => s.Cuenta).Select(g => (Cuenta: g.Key, Saldo: g.Sum(s => s.Importe))).Where(c => c.Saldo != 0m).ToList();
        var sinMapear = porCuenta.Where(c => !rubroPorCuenta.ContainsKey(c.Cuenta)).Select(c => c.Cuenta).Order(StringComparer.Ordinal).ToList();
        var mapeadas = porCuenta.Where(c => rubroPorCuenta.ContainsKey(c.Cuenta)).Select(c => (Clase: ReglasRubros.Clase(rubroPorCuenta[c.Cuenta]), c.Saldo)).ToList();

        // Débitos positivos y créditos negativos (como SAP): activo − (pasivo + patrimonio) es la suma de las cuentas de balance.
        var balance = mapeadas.Where(m => ReglasRubros.EsBalance(m.Clase)).Sum(m => m.Saldo);
        var utilidad = mapeadas.Where(m => ReglasRubros.EsResultados(m.Clase)).Sum(m => m.Saldo);
        var resultado = mapeadas.Where(m => m.Clase == ClaseCuenta.ResultadoEjercicio).Sum(m => m.Saldo);
        var diferenciaResultado = utilidad - resultado;

        var importable = Math.Abs(balance) <= tolerancia && Math.Abs(diferenciaResultado) <= tolerancia && sinMapear.Count == 0;
        return new ValidacionCuadre(sociedad, periodo, importable, balance, diferenciaResultado, sinMapear);
    }

    /// <summary>Motivo para UX-09 y C03 de una sociedad que no se importa.</summary>
    public static string Motivo(ValidacionCuadre v, decimal tolerancia)
    {
        var motivos = new List<string>();
        if (Math.Abs(v.DiferenciaBalance) > tolerancia)
        {
            motivos.Add($"activo − (pasivo + patrimonio) = {Formato(v.DiferenciaBalance)}");
        }

        if (Math.Abs(v.DiferenciaResultado) > tolerancia)
        {
            motivos.Add($"utilidad − resultado del ejercicio = {Formato(v.DiferenciaResultado)}");
        }

        if (v.CuentasSinMapear.Count > 0)
        {
            motivos.Add($"cuentas con saldo sin mapear: {string.Join(", ", v.CuentasSinMapear)}");
        }

        return $"{v.Sociedad} no se importa ({v.Periodo}): {string.Join("; ", motivos)}.";
    }

    public static string Formato(decimal valor) => valor.ToString("#,##0.00", System.Globalization.CultureInfo.GetCultureInfo("es-CO"));
}
