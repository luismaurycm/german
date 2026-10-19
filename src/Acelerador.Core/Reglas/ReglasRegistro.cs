using System.Globalization;
using System.Text;
using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>
/// RNF-Aud-01 y HU-54: retención del registro de ejecuciones y archivo de descarga. Las entradas se conservan
/// al menos 12 meses; la depuración solo quita las que pasaron de 12 meses.
/// </summary>
public static class ReglasRegistro
{
    public const int MesesRetencion = 12;

    public static readonly IReadOnlyList<string> Columnas = ["Fecha y hora", "Usuario", "Sistema", "Paso", "Objetos", "Resultado", "Detalle"];

    /// <summary>Las entradas anteriores a esta fecha ya cumplieron los 12 meses y se pueden depurar.</summary>
    public static DateTimeOffset Corte(DateTimeOffset ahora) => ahora.AddMonths(-MesesRetencion);

    public static bool SeConserva(Ejecucion e, DateTimeOffset ahora) => e.Fecha >= Corte(ahora);

    /// <summary>
    /// CSV con las mismas columnas de la pantalla, separado por punto y coma (Excel en español) y con BOM UTF-8.
    /// Las celdas que empiezan por =, +, -, @ o tabulador llevan un apóstrofo delante para que Excel no las evalúe.
    /// </summary>
    public static byte[] Csv(IEnumerable<Ejecucion> ejecuciones)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(';', Columnas.Select(Celda))).Append("\r\n");
        foreach (var e in ejecuciones)
        {
            var fecha = e.Fecha.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
            string[] valores = [fecha, e.Usuario, e.Sistema, e.Paso, e.Objetos, Textos.Resultado(e.Resultado), e.Mensaje];
            sb.Append(string.Join(';', valores.Select(Celda))).Append("\r\n");
        }

        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Celda(string valor)
    {
        var v = valor.Length > 0 && "=+-@\t\r".Contains(valor[0], StringComparison.Ordinal) ? "'" + valor : valor;
        return "\"" + v.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
