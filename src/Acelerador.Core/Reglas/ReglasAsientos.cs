using System.Globalization;
using System.Text.RegularExpressions;
using Acelerador.Core.Consolidacion;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>Una línea de UX-14 tal como la escribe el Usuario financiero.</summary>
public sealed record LineaAsientoTexto(string? Sociedad, string? Cuenta, string? Intco, string? Debito, string? Credito, string? Observacion)
{
    public bool Vacia => new[] { Sociedad, Cuenta, Intco, Debito, Credito, Observacion }.All(string.IsNullOrWhiteSpace);
}

/// <summary>Error de una celda de UX-14: número de línea (desde 1), dato y motivo.</summary>
public sealed record ErrorLineaAsiento(int Linea, string Dato, string Motivo);

/// <summary>El asiento leído: las líneas válidas, los errores por celda y los totales (RN-15).</summary>
public sealed record AsientoValidado(IReadOnlyList<LineaAsiento> Lineas, IReadOnlyList<ErrorLineaAsiento> Errores, decimal Debitos, decimal Creditos, int LineasEscritas)
{
    public decimal Diferencia => Debitos - Creditos;

    public bool Cuadra => LineasEscritas > 0 && Diferencia == 0m;

    public bool Guardable => Cuadra && Errores.Count == 0;
}

/// <summary>HU-39: asientos de ajuste de consolidación (RF-35) con débitos iguales a créditos (RN-15) y sin sociedades bloqueadas (RN-17).</summary>
public static partial class ReglasAsientos
{
    public const string Sociedad = "sociedad";
    public const string Cuenta = "cuenta";
    public const string Intco = "intco";
    public const string Debito = "debito";
    public const string Credito = "credito";

    public const string Vacio = "Aún no hay líneas: usa Agregar línea.";
    public const string NoCuadra = "El asiento no cuadra. Los débitos deben ser iguales a los créditos para guardar (RN-15).";

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>«Cuadrado» o «Descuadrado: 1.000,00» (UX-14, con texto y no solo color).</summary>
    public static string Indicador(AsientoValidado a) =>
        a.LineasEscritas == 0 ? "—" : a.Diferencia == 0m ? "Cuadrado" : $"Descuadrado: {ReglaCuadre.Formato(Math.Abs(a.Diferencia))}";

    /// <summary>Lee un importe con formato colombiano (12.000.000 o 1.500,50) o con punto decimal (1500.50); vacío es 0.</summary>
    public static bool TryLeerImporte(string? texto, out decimal valor)
    {
        var t = (texto ?? "").Trim().Replace(" ", "", StringComparison.Ordinal).Replace("$", "", StringComparison.Ordinal);
        if (t.Length == 0)
        {
            valor = 0m;
            return true;
        }

        if (t.Contains(',', StringComparison.Ordinal) || MilesConPunto().IsMatch(t))
        {
            return decimal.TryParse(t, NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, Colombia, out valor);
        }

        return decimal.TryParse(t, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out valor);
    }

    public static AsientoValidado Validar(
        IReadOnlyList<LineaAsientoTexto> lineas,
        IReadOnlySet<string> sociedades,
        IReadOnlySet<string> cuentas,
        IReadOnlySet<string> bloqueadas,
        string periodo)
    {
        var validas = new List<LineaAsiento>();
        var errores = new List<ErrorLineaAsiento>();
        decimal debitos = 0m, creditos = 0m;
        var escritas = 0;
        for (var i = 0; i < lineas.Count; i++)
        {
            var l = lineas[i];
            if (l.Vacia)
            {
                continue;
            }

            escritas++;
            var n = i + 1;
            var propios = new List<ErrorLineaAsiento>();
            var sociedad = (l.Sociedad ?? "").Trim().ToUpperInvariant();
            var cuenta = (l.Cuenta ?? "").Trim().ToUpperInvariant();
            var intco = string.IsNullOrWhiteSpace(l.Intco) ? MotorConsolidacion.IntcoNinguno : l.Intco.Trim().ToUpperInvariant();
            if (!sociedades.Contains(sociedad))
            {
                propios.Add(new(n, Sociedad, sociedad.Length == 0 ? "Falta la sociedad" : $"{sociedad} no es una sociedad del grupo"));
            }
            else if (bloqueadas.Contains(sociedad))
            {
                propios.Add(new(n, Sociedad, ReglasCierreMensual.MensajeBloqueada(sociedad, periodo, "sus líneas no se aceptan")));
            }

            if (!cuentas.Contains(cuenta))
            {
                propios.Add(new(n, Cuenta, cuenta.Length == 0 ? "Falta la cuenta" : $"{cuenta} no es una cuenta del modelo de consolidación"));
            }

            if (intco != MotorConsolidacion.IntcoNinguno && (!intco.StartsWith("I_", StringComparison.Ordinal) || !sociedades.Contains(intco[2..]) || intco[2..] == sociedad))
            {
                propios.Add(new(n, Intco, "La intercompañía es I_NONE o I_ y otra sociedad del grupo"));
            }

            var debitoOk = TryLeerImporte(l.Debito, out var debito) && debito >= 0m;
            var creditoOk = TryLeerImporte(l.Credito, out var credito) && credito >= 0m;
            if (!debitoOk)
            {
                propios.Add(new(n, Debito, "El débito es un importe positivo"));
            }

            if (!creditoOk)
            {
                propios.Add(new(n, Credito, "El crédito es un importe positivo"));
            }

            if (debitoOk && creditoOk && (debito == 0m) == (credito == 0m))
            {
                propios.Add(new(n, debito == 0m ? Debito : Credito, "Cada línea lleva un débito o un crédito"));
            }

            debitos += debitoOk ? debito : 0m;
            creditos += creditoOk ? credito : 0m;
            if (propios.Count == 0)
            {
                validas.Add(new LineaAsiento(sociedad, cuenta, intco, debito, credito, (l.Observacion ?? "").Trim()));
            }

            errores.AddRange(propios);
        }

        return new AsientoValidado(validas, errores, debitos, creditos, escritas);
    }

    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+$")]
    private static partial Regex MilesConPunto();
}
