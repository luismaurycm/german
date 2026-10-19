using Acelerador.Sap;

namespace Acelerador.Core.Reportes;

/// <summary>
/// HU-53 y HU-46 CA2: reportes de activos fijos (A01 a A03) y deterioros (D01, D02), que leen BW (RN-12) en la moneda local
/// de cada sociedad, y los movimientos de BW que completan las notas R08, R10 y R11 (RF-30).
/// </summary>
public static class MotorBw
{
    public const string InfoProviderActivos = "ZACC_AF";
    public const string InfoProviderDeterioros = "ZACC_DE";

    /// <summary>Palabras que marcan una clase de activo intangible (R11); el resto es propiedad, planta y equipo (R10).</summary>
    public static bool EsIntangible(string clase) =>
        new[] { "Intangible", "Software", "Licencia", "Marca" }.Any(p => clase.Contains(p, StringComparison.OrdinalIgnoreCase));

    private static readonly (string Concepto, string Titulo)[] ConceptosActivos =
        [("SALDO_INICIAL", "Saldo inicial"), ("ADICION", "Adiciones"), ("RETIRO", "Retiros"), ("DEPRECIACION", "Depreciación del periodo")];

    private static readonly (string Concepto, string Titulo)[] ConceptosDeterioro =
        [("SALDO_INICIAL", "Saldo inicial"), ("DETERIORO", "Deterioro del periodo"), ("RECUPERACION", "Recuperaciones"), ("CASTIGO", "Castigos")];

    public static ReporteCalculado Calcular(DefinicionReporte d, ParametrosReporte p, IReadOnlyList<RegistroBw> registros) => d.Id switch
    {
        "A01" => Movimientos(d, p, registros.Where(r => r.Activo is null && r.Concepto != "DEPRECIACION_ACUMULADA").ToList(), ConceptosActivos, "Clase de activo"),
        "A02" => A02(d, p, registros),
        "A03" => A03(d, p, registros),
        "D01" => Movimientos(d, p, registros, ConceptosDeterioro, "Cuenta"),
        "D02" => D02(d, p, registros),
        _ => throw new ArgumentOutOfRangeException(nameof(d), d.Id, "Reporte sin cálculo de BW."),
    };

    /// <summary>Saldo inicial, movimientos y saldo final por clase o cuenta (A01, D01 y el complemento de R08, R10 y R11).</summary>
    public static ReporteCalculado Movimientos(DefinicionReporte d, ParametrosReporte p, IReadOnlyList<RegistroBw> registros, IReadOnlyList<(string Concepto, string Titulo)> conceptos, string etiqueta)
    {
        var propios = registros.Where(r => r.Sociedad == p.Sociedad && r.Activo is null).ToList();
        var columnas = conceptos.Select(c => new ColumnaReporte(c.Concepto, c.Titulo)).Append(new ColumnaReporte("SALDO_FINAL", "Saldo final")).ToList();
        var filas = propios.GroupBy(r => r.Clase).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g =>
            {
                var valores = conceptos.Select(c => g.Where(r => r.Concepto == c.Concepto).Sum(r => r.Importe)).ToList();
                return new FilaReporte(TipoFila.Detalle, [g.Key], [.. valores, valores.Sum()]);
            }).ToList();
        if (filas.Count > 0)
        {
            filas.Add(new FilaReporte(TipoFila.Total, ["Total"], Enumerable.Range(0, columnas.Count).Select(i => filas.Sum(f => f.Valores[i])).ToList()));
        }

        return new ReporteCalculado(d, p, [etiqueta], columnas, filas, [Moneda(propios)], [], filas.Count == 0);
    }

    /// <summary>Movimientos de activos fijos de R10 (sin intangibles) o R11 (solo intangibles).</summary>
    public static ReporteCalculado MovimientosActivos(DefinicionReporte d, ParametrosReporte p, IReadOnlyList<RegistroBw> registros, bool intangibles) =>
        Movimientos(d, p, registros.Where(r => r.Activo is null && r.Concepto != "DEPRECIACION_ACUMULADA" && EsIntangible(r.Clase) == intangibles).ToList(), ConceptosActivos, "Clase de activo");

    public static ReporteCalculado MovimientosDeterioro(DefinicionReporte d, ParametrosReporte p, IReadOnlyList<RegistroBw> registros) =>
        Movimientos(d, p, registros, ConceptosDeterioro, "Cuenta");

    private static ReporteCalculado A02(DefinicionReporte d, ParametrosReporte p, IReadOnlyList<RegistroBw> registros)
    {
        var propios = registros.Where(r => r.Sociedad == p.Sociedad && r.Activo is null).ToList();
        ColumnaReporte[] columnas = [new("DEPRECIACION", "Depreciación del periodo"), new("DEPRECIACION_ACUMULADA", "Depreciación acumulada")];
        var filas = propios.GroupBy(r => r.Clase).OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new FilaReporte(TipoFila.Detalle, [g.Key], columnas.Select(c => g.Where(r => r.Concepto == c.Id).Sum(r => r.Importe)).ToList()))
            .ToList();
        if (filas.Count > 0)
        {
            filas.Add(new FilaReporte(TipoFila.Total, ["Total"], [filas.Sum(f => f.Valores[0]), filas.Sum(f => f.Valores[1])]));
        }

        return new ReporteCalculado(d, p, ["Clase de activo"], columnas, filas, [Moneda(propios)], [], filas.Count == 0);
    }

    private static ReporteCalculado A03(DefinicionReporte d, ParametrosReporte p, IReadOnlyList<RegistroBw> registros)
    {
        var activos = registros.Where(r => r.Sociedad == p.Sociedad && r.Activo is not null).ToList();
        ColumnaReporte[] columnas = [new("COSTO", "Costo"), new("DEPRECIACION_ACUMULADA", "Depreciación acumulada"), new("NETO", "Valor neto")];
        var filas = activos.GroupBy(r => (r.Activo!, r.Clase, r.Descripcion)).OrderBy(g => g.Key.Item1, StringComparer.Ordinal)
            .Select(g =>
            {
                var costo = g.Where(r => r.Concepto == "COSTO").Sum(r => r.Importe);
                var dep = g.Where(r => r.Concepto == "DEPRECIACION_ACUMULADA").Sum(r => r.Importe);
                return new FilaReporte(TipoFila.Detalle, [g.Key.Item1, g.Key.Clase, g.Key.Descripcion], [costo, dep, costo + dep]);
            }).ToList();
        if (filas.Count > 0)
        {
            filas.Add(new FilaReporte(TipoFila.Total, ["", "", "Total"], Enumerable.Range(0, 3).Select(i => filas.Sum(f => f.Valores[i])).ToList()));
        }

        return new ReporteCalculado(d, p, ["Activo", "Clase", "Descripción"], columnas, filas, [Moneda(activos)], [], filas.Count == 0);
    }

    private static ReporteCalculado D02(DefinicionReporte d, ParametrosReporte p, IReadOnlyList<RegistroBw> registros)
    {
        ColumnaReporte[] columnas = [new("DETERIORO", "Deterioro del periodo"), new("SALDO_FINAL", "Saldo del deterioro")];
        var filas = registros.Where(r => r.Activo is null).GroupBy(r => (r.Sociedad, r.Clase, r.Moneda))
            .OrderBy(g => g.Key.Sociedad, StringComparer.Ordinal).ThenBy(g => g.Key.Clase, StringComparer.Ordinal)
            .Select(g => new FilaReporte(TipoFila.Detalle, [g.Key.Sociedad, g.Key.Clase, g.Key.Moneda], [g.Where(r => r.Concepto == "DETERIORO").Sum(r => r.Importe), g.Sum(r => r.Importe)]))
            .ToList();
        return new ReporteCalculado(d, p, ["Sociedad", "Cuenta", "Moneda"], columnas, filas, filas.Count == 0 ? [] : ["Valores en la moneda local de cada sociedad, leídos de BW (RN-12)."], [], filas.Count == 0);
    }

    private static string Moneda(IReadOnlyList<RegistroBw> registros) =>
        registros.Count == 0 ? "Datos de BW (RN-12)." : $"Valores en {registros[0].Moneda}, moneda local de la sociedad, leídos de BW (RN-12).";
}
