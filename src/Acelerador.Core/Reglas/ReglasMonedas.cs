namespace Acelerador.Core.Reglas;

/// <summary>RN-06 y RN-11: la consolidación se presenta en moneda local y en dólares, con tasa por tipo de cuenta.</summary>
public static class ReglasMonedas
{
    public const string Dolar = "USD";

    /// <summary>RN-11 (por confirmar en la pregunta 3 del DAF): cierre para balance, promedio para resultados e histórica para patrimonio.</summary>
    public static readonly IReadOnlyList<(string Tipo, string Aplica)> TiposDeTasa =
    [
        ("Cierre", "Cuentas de balance (activo y pasivo)"),
        ("Promedio", "Cuentas de resultados"),
        ("Histórica", "Cuentas de patrimonio"),
    ];
}
