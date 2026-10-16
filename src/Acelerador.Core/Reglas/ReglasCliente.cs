using System.Globalization;
using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>Reglas de HU-02: nombre obligatorio y único, y búsqueda por nombre.</summary>
public static class ReglasCliente
{
    public const int LongitudMaxima = 80;

    private static readonly CompareInfo Comparador = CultureInfo.GetCultureInfo("es-CO").CompareInfo;
    private const CompareOptions SinMayusculasNiTildes = CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace;

    /// <summary>Devuelve el mensaje de error o null si el nombre es válido (HU-02 CA2).</summary>
    public static string? ValidarNombre(string? nombre, IEnumerable<string> existentes)
    {
        var limpio = Normalizar(nombre);
        if (limpio.Length == 0)
        {
            return "El nombre del cliente es obligatorio.";
        }

        if (limpio.Length > LongitudMaxima)
        {
            return $"El nombre no puede pasar de {LongitudMaxima} caracteres.";
        }

        if (existentes.Any(e => Comparador.Compare(Normalizar(e), limpio, SinMayusculasNiTildes) == 0))
        {
            return $"Ya existe un cliente con el nombre «{limpio}».";
        }

        return null;
    }

    /// <summary>Quita espacios sobrantes al inicio, al final y entre palabras.</summary>
    public static string Normalizar(string? nombre) =>
        string.Join(' ', (nombre ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>Filtra por nombre sin distinguir mayúsculas ni tildes (HU-02 CA5).</summary>
    public static IReadOnlyList<Cliente> Filtrar(IEnumerable<Cliente> clientes, string? busqueda)
    {
        var q = Normalizar(busqueda);
        var lista = clientes.OrderBy(c => c.Nombre, StringComparer.Create(CultureInfo.GetCultureInfo("es-CO"), true));
        return q.Length == 0
            ? lista.ToList()
            : lista.Where(c => Comparador.IndexOf(c.Nombre, q, SinMayusculasNiTildes) >= 0).ToList();
    }
}
