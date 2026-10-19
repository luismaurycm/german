using System.Globalization;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Reportes;

/// <summary>Lo que leen P01 a P05 del modelo de planeación: los registros del año y el mapeo para agrupar por rubro.</summary>
public sealed record DatosPresupuesto(IReadOnlyList<RegistroBpc> Registros, IReadOnlyList<AsignacionCuenta> Asignaciones, int MesCierre);

/// <summary>
/// HU-52: P01 a P04 sobre el modelo de planeación (RN-08). El presupuesto es la versión elegida (HU-51) y la ejecución la
/// categoría ACTUAL acumulada hasta el mes de cierre; ingresos y gastos se presentan en positivo.
/// </summary>
public static class MotorPresupuesto
{
    public const string Modelo = "PLANEACION";
    public const string Real = "ACTUAL";

    public static int Mes(string periodo) => int.Parse(periodo.Replace('-', '.')[5..], CultureInfo.InvariantCulture);

    public static ReporteCalculado Calcular(DefinicionReporte d, ParametrosReporte p, DatosPresupuesto datos)
    {
        var rubroDe = datos.Asignaciones.GroupBy(a => GeneradorModeloBpc.IdCuenta(a.PlanCuentas, a.Cuenta)).ToDictionary(g => g.Key, g => g.First().Rubro);
        var propios = datos.Registros.Where(r => r.Modelo == Modelo && r.Entidad == p.Sociedad).ToList();
        decimal Presentar(RegistroBpc r) => rubroDe.TryGetValue(r.Cuenta, out var rubro) ? MotorReportes.Signo(rubro) * r.Importe : r.Importe;
        var presupuesto = propios.Where(r => r.Categoria == p.Version).ToList();
        var real = propios.Where(r => r.Categoria == Real && Mes(r.Periodo) <= datos.MesCierre).ToList();
        var sinDatos = propios.Count == 0;
        return d.Id switch
        {
            "P01" => Comparacion(d, p, ["Rubro", "Concepto"], presupuesto, real, r => rubroDe.GetValueOrDefault(r.Cuenta) ?? "—", Presentar, rubro => CatalogoRubros.Buscar(rubro)?.Nombre ?? rubro, sinDatos, total: true),
            "P02" => Comparacion(d, p, ["Centro de costo", "Concepto"], presupuesto.Where(r => Gasto(rubroDe, r)).ToList(), real.Where(r => Gasto(rubroDe, r)).ToList(), r => r.Centro, Presentar, _ => "Gastos", sinDatos, total: true),
            "P03" => Comparacion(d, p, ["Centro de beneficio", "Concepto"], presupuesto.Where(r => !Gasto(rubroDe, r)).ToList(), real.Where(r => !Gasto(rubroDe, r)).ToList(), r => r.Centro, Presentar, _ => "Ingresos", sinDatos, total: true),
            "P04" => Proyeccion(d, p, presupuesto, real, datos.MesCierre, rubroDe, Presentar, sinDatos),
            _ => throw new ArgumentOutOfRangeException(nameof(d), d.Id, "Reporte de presupuesto sin cálculo."),
        };
    }

    private static bool Gasto(IReadOnlyDictionary<string, string> rubroDe, RegistroBpc r) =>
        rubroDe.TryGetValue(r.Cuenta, out var rubro) && ReglasRubros.Clase(rubro) == ClaseCuenta.Gasto;

    public static decimal? PorcentajeEjecucion(decimal presupuesto, decimal ejecucion) => presupuesto == 0m ? null : decimal.Round(ejecucion / presupuesto * 100m, 2);

    private static ReporteCalculado Comparacion(DefinicionReporte d, ParametrosReporte p, IReadOnlyList<string> encabezados, IReadOnlyList<RegistroBpc> presupuesto, IReadOnlyList<RegistroBpc> real,
        Func<RegistroBpc, string> clave, Func<RegistroBpc, decimal> presentar, Func<string, string> descripcion, bool sinDatos, bool total)
    {
        ColumnaReporte[] columnas = [new("PRES", "Presupuesto"), new("EJEC", "Ejecución"), new("VAR", "Variación"), new("PCT", "% ejecución", Porcentaje: true)];
        var claves = presupuesto.Concat(real).Select(clave).Distinct().Order(StringComparer.Ordinal).ToList();
        FilaReporte Fila(TipoFila tipo, string codigo, string texto, decimal pres, decimal ejec) =>
            new(tipo, [codigo, texto], [pres, ejec, ejec - pres, PorcentajeEjecucion(pres, ejec) ?? 0m], tipo == TipoFila.Rubro && codigo.StartsWith("F-", StringComparison.Ordinal) ? codigo : null);
        var filas = claves.Select(k => Fila(TipoFila.Rubro, k, descripcion(k), presupuesto.Where(r => clave(r) == k).Sum(presentar), real.Where(r => clave(r) == k).Sum(presentar))).ToList();
        if (total && filas.Count > 0)
        {
            filas.Add(Fila(TipoFila.Total, "", "Total", filas.Sum(f => f.Valores[0]), filas.Sum(f => f.Valores[1])));
        }

        return new ReporteCalculado(d, p, encabezados, columnas, filas, [], [], sinDatos);
    }

    private static ReporteCalculado Proyeccion(DefinicionReporte d, ParametrosReporte p, IReadOnlyList<RegistroBpc> presupuesto, IReadOnlyList<RegistroBpc> real, int mesCierre,
        IReadOnlyDictionary<string, string> rubroDe, Func<RegistroBpc, decimal> presentar, bool sinDatos)
    {
        ColumnaReporte[] columnas = [new("EJEC", $"Ejecución acumulada a {mesCierre:00}"), new("REST", "Presupuesto restante"), new("PROY", "Proyección de cierre"), new("PRES", "Presupuesto anual"), new("DESV", "Desviación proyectada")];
        string Rubro(RegistroBpc r) => rubroDe.GetValueOrDefault(r.Cuenta) ?? "—";
        var rubros = presupuesto.Concat(real).Select(Rubro).Distinct().Order(StringComparer.Ordinal).ToList();
        var filas = rubros.Select(rubro =>
        {
            var ejec = real.Where(r => Rubro(r) == rubro).Sum(presentar);
            var resto = presupuesto.Where(r => Rubro(r) == rubro && Mes(r.Periodo) > mesCierre).Sum(presentar);
            var anual = presupuesto.Where(r => Rubro(r) == rubro).Sum(presentar);
            return new FilaReporte(TipoFila.Rubro, [rubro, CatalogoRubros.Buscar(rubro)?.Nombre ?? rubro], [ejec, resto, ejec + resto, anual, ejec + resto - anual], rubro);
        }).ToList();
        if (filas.Count > 0)
        {
            filas.Add(new FilaReporte(TipoFila.Total, ["", "Total"], Enumerable.Range(0, 5).Select(i => filas.Sum(f => f.Valores[i])).ToList()));
        }

        return new ReporteCalculado(d, p, ["Rubro", "Concepto"], columnas, filas, [], [], sinDatos);
    }
}
