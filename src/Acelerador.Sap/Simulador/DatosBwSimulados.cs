using System.Globalization;
using System.Text.RegularExpressions;

namespace Acelerador.Sap.Simulador;

/// <summary>Saldo del ERP en BW (0FI_GL_14) para el presupuesto: cuenta, centro de costo o de beneficio, mes y categoría.</summary>
public sealed record SaldoPlan(string Cuenta, string Centro, string Periodo, string Categoria, decimal Importe);

/// <summary>
/// Datos de BW del cliente de ejemplo que no viven en BPC (RN-12): movimientos de activos fijos (0FI_AA_11), deterioro de
/// cartera (0FI_AR_4) y presupuesto y ejecución por centro (0FI_GL_14). Salen del plan de cuentas de ejemplo para que
/// cuadren con los saldos: la suma de las clases de activo es la propiedad, planta y equipo de la sociedad.
/// </summary>
public sealed partial class SimulatedSapGateway
{
    public const string CategoriaReal = "ACTUAL";
    public const string CategoriaPlanErp = "PLAN";

    public static string CentroCosto(string sociedad, int n) => $"{sociedad}CC{n:00}";

    public static string CentroBeneficio(string sociedad, int n) => $"{sociedad}PC{n:00}";

    public async Task<ResultadoConsultaBw> ConsultarBwAsync(DestinoSap bw, ConsultaBw consulta, CancellationToken ct = default)
    {
        var o = _opciones();
        Registrar($"Z_ACC_BW_LEER {consulta.InfoProvider}", SistemaSap.Bw);
        await Esperar(o, ct);
        if (o.SistemasConFallo.Contains(SistemaSap.Bw) || Contiene(bw.Parametros.Host, "falla"))
        {
            return new ResultadoConsultaBw(false, MensajeNoAlcanzable(bw.Parametros.Host), []);
        }

        // El paquete Z resuelve el InfoProvider del módulo (ZACC_AF_D01 en BW/4HANA, ZACC_AF_C01 en BW 7.5).
        var nombre = new Regex($"^{Regex.Escape(consulta.InfoProvider)}_[DC]01$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        var creado = _objetos.Values.Any(x => x.Sistema == SistemaSap.Bw && x.Operacion.TipoObjeto is "ADSO" or "INFOCUBO" && nombre.IsMatch(x.Operacion.NombreTecnico));
        if (!creado)
        {
            return new ResultadoConsultaBw(false, $"RSDRI: el InfoProvider {consulta.InfoProvider} no existe en BW; ejecuta el plan con el módulo.", []);
        }

        if (consulta.Periodo != PeriodoSaldos)
        {
            return new ResultadoConsultaBw(true, "0 registros.", []);
        }

        var sociedades = SociedadesErp.Where(s => consulta.Sociedad is null || s.Codigo == consulta.Sociedad).ToList();
        var registros = consulta.InfoProvider.StartsWith("ZACC_AF", StringComparison.OrdinalIgnoreCase)
            ? sociedades.SelectMany(s => ActivosFijos(s, o)).ToList()
            : sociedades.SelectMany(s => Deterioro(s, o)).ToList();
        return new ResultadoConsultaBw(true, $"{registros.Count} registros.", registros);
    }

    private static decimal Saldo(SociedadErp s, string cuenta, OpcionesSimulador o) =>
        SaldosErp(s.Codigo, o).Where(x => x.Cuenta == cuenta).Sum(x => x.Importe);

    /// <summary>Activos fijos por clase (una por cuenta de propiedad, planta y equipo) y dos activos por clase, en moneda local.</summary>
    private static IEnumerable<RegistroBw> ActivosFijos(SociedadErp s, OpcionesSimulador o)
    {
        var cuentas = Cuentas.Where(c => c.Plan == s.PlanCuentas && c.Nodo == "Propiedad, planta y equipo").ToList();
        var depreciacion = cuentas.Where(c => c.Saldo < 0).Sum(c => Saldo(s, c.Numero, o));
        var clases = cuentas.Where(c => c.Saldo > 0).Select(c => (c.Descripcion, Costo: Saldo(s, c.Numero, o), Depreciable: !c.Descripcion.StartsWith("Terrenos", StringComparison.Ordinal))).ToList();
        var baseDepreciable = clases.Where(c => c.Depreciable).Sum(c => c.Costo);
        var p = PeriodoSaldos;
        foreach (var (clase, costo, depreciable) in clases)
        {
            var acumulada = depreciable && baseDepreciable != 0m ? decimal.Round(depreciacion * costo / baseDepreciable, 2) : 0m;
            var final = costo + acumulada;
            var adiciones = decimal.Round(costo * 0.06m, 2);
            var retiros = decimal.Round(-costo * 0.01m, 2);
            var delPeriodo = depreciable ? decimal.Round(acumulada * 0.1m, 2) : 0m;
            yield return new RegistroBw(s.Codigo, clase, null, "SALDO_INICIAL", p, s.Moneda, final - adiciones - retiros - delPeriodo);
            yield return new RegistroBw(s.Codigo, clase, null, "ADICION", p, s.Moneda, adiciones);
            yield return new RegistroBw(s.Codigo, clase, null, "RETIRO", p, s.Moneda, retiros);
            yield return new RegistroBw(s.Codigo, clase, null, "DEPRECIACION", p, s.Moneda, delPeriodo);
            yield return new RegistroBw(s.Codigo, clase, null, "DEPRECIACION_ACUMULADA", p, s.Moneda, acumulada);
            for (var i = 1; i <= 2; i++)
            {
                var parte = i == 1 ? 0.6m : 0.4m;
                var activo = $"{s.Codigo}-{clase[..3].ToUpperInvariant()}-{i:000}";
                var fecha = new DateOnly(2020 + i, i * 3, 1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                yield return new RegistroBw(s.Codigo, clase, activo, "COSTO", p, s.Moneda, decimal.Round(costo * parte, 2), $"{clase} {i} · capitalizado {fecha}");
                yield return new RegistroBw(s.Codigo, clase, activo, "DEPRECIACION_ACUMULADA", p, s.Moneda, decimal.Round(acumulada * parte, 2), $"{clase} {i} · capitalizado {fecha}");
            }
        }
    }

    /// <summary>Deterioro de cartera por cuenta de clientes: 3 % del saldo al cierre, con el deterioro, recuperaciones y castigos del periodo.</summary>
    private static IEnumerable<RegistroBw> Deterioro(SociedadErp s, OpcionesSimulador o)
    {
        foreach (var c in Cuentas.Where(c => c.Plan == s.PlanCuentas && c.Nodo == "Deudores" && (c.Descripcion.StartsWith("Clientes", StringComparison.Ordinal) || c.Descripcion.StartsWith("Cuentas por cobrar", StringComparison.Ordinal))))
        {
            var cartera = Saldo(s, c.Numero, o);
            var final = decimal.Round(-cartera * 0.03m, 2);
            var deterioro = decimal.Round(-cartera * 0.008m, 2);
            var recuperaciones = decimal.Round(cartera * 0.001m, 2);
            var castigos = decimal.Round(cartera * 0.002m, 2);
            var p = PeriodoSaldos;
            var clase = $"{c.Numero} {c.Descripcion}";
            yield return new RegistroBw(s.Codigo, clase, null, "SALDO_INICIAL", p, s.Moneda, final - deterioro - recuperaciones - castigos);
            yield return new RegistroBw(s.Codigo, clase, null, "DETERIORO", p, s.Moneda, deterioro);
            yield return new RegistroBw(s.Codigo, clase, null, "RECUPERACION", p, s.Moneda, recuperaciones);
            yield return new RegistroBw(s.Codigo, clase, null, "CASTIGO", p, s.Moneda, castigos);
        }
    }

    /// <summary>
    /// Presupuesto (versión 0 del ERP) y ejecución por cuenta de resultados y centro, mes a mes: la ejecución reparte el saldo
    /// acumulado del ERP en los meses cerrados del año; el presupuesto es un 5 % más, en los doce meses.
    /// </summary>
    public static IReadOnlyList<SaldoPlan> PlanErp(string sociedad, int anio, OpcionesSimulador o)
    {
        var soc = SociedadesErp.FirstOrDefault(x => x.Codigo == sociedad);
        if (soc is null || anio != int.Parse(PeriodoSaldos[..4], CultureInfo.InvariantCulture))
        {
            return [];
        }

        var meses = int.Parse(PeriodoSaldos[5..], CultureInfo.InvariantCulture);
        var (cecos, cebes) = Centros.GetValueOrDefault(sociedad, (1, 1));
        var lista = new List<SaldoPlan>();
        var resultados = Cuentas.Where(c => c.Plan == soc.PlanCuentas && c.Nodo is "Ingresos operacionales" or "Gastos de administración" or "Gastos de ventas" or "Gastos no operacionales").ToList();
        for (var i = 0; i < resultados.Count; i++)
        {
            var c = resultados[i];
            var ingreso = c.Nodo == "Ingresos operacionales";
            var centro = ingreso ? CentroBeneficio(sociedad, (i % Math.Max(cebes, 1)) + 1) : CentroCosto(sociedad, (i % Math.Max(cecos, 1)) + 1);
            var total = Saldo(soc, c.Numero, o);
            var mensual = decimal.Round(total / meses, 2);
            for (var m = 1; m <= meses; m++)
            {
                lista.Add(new SaldoPlan(c.Numero, centro, $"{anio}-{m:00}", CategoriaReal, m == meses ? total - mensual * (meses - 1) : mensual));
            }

            var plan = decimal.Round(mensual * 1.05m, 2);
            for (var m = 1; m <= 12; m++)
            {
                lista.Add(new SaldoPlan(c.Numero, centro, $"{anio}-{m:00}", CategoriaPlanErp, plan));
            }
        }

        return lista;
    }
}
