namespace Acelerador.Core.Dominio;

/// <summary>Una fila del archivo contable de una sociedad no SAP (RF-15, INT-04). <see cref="Fila"/> es la línea del archivo.</summary>
public sealed record FilaNoSap(
    int Fila,
    string Sociedad,
    string Cuenta,
    string Periodo,
    string Tercero,
    string CentroCosto,
    string CentroBeneficio,
    decimal Importe);

/// <summary>Error de validación del archivo: fila (0 = el archivo completo), columna y motivo (HU-25 CA2).</summary>
public sealed record ErrorArchivo(int Fila, string Columna, string Motivo);

/// <summary>Resultado de leer y validar un archivo, antes de confirmarlo.</summary>
public sealed record ArchivoNoSap(
    string Sociedad,
    string Nombre,
    IReadOnlyList<FilaNoSap> Filas,
    IReadOnlyList<ErrorArchivo> Errores,
    int TotalErrores)
{
    public bool Valido => TotalErrores == 0 && Filas.Count > 0;

    public IReadOnlyList<string> Periodos => Filas.Select(f => f.Periodo).Distinct().Order(StringComparer.Ordinal).ToList();

    public decimal Total => Filas.Sum(f => f.Importe);
}

/// <summary>
/// Carga confirmada de una sociedad no SAP para un periodo. Solo metadatos: los importes quedan en BW, no en la base
/// local (arquitectura, sección 7).
/// </summary>
public sealed record CargaNoSap(
    Guid ClienteId,
    string Sociedad,
    string Periodo,
    string Archivo,
    int Filas,
    decimal Total,
    DateTimeOffset Fecha,
    string Usuario);
