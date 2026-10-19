using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>Una fila de C02 tal como la escribe el Usuario financiero.</summary>
public sealed record FilaParticipacion(string Sociedad, string? Control, string? Consolidacion, string? Propiedad, string? Metodo);

/// <summary>Error de una celda de C02: sociedad, dato y motivo.</summary>
public sealed record ErrorParticipacion(string Sociedad, string Dato, string Motivo);

/// <summary>HU-35 CA3: porcentajes de 0 a 100 y método obligatorio; una fila con error no se guarda.</summary>
public static class ReglasParticipaciones
{
    public const string Control = "control";
    public const string Consolidacion = "consolidacion";
    public const string Propiedad = "propiedad";
    public const string Metodo = "metodo";

    public static (IReadOnlyList<Participacion> Validas, IReadOnlyList<ErrorParticipacion> Errores) Validar(IEnumerable<FilaParticipacion> filas)
    {
        var validas = new List<Participacion>();
        var errores = new List<ErrorParticipacion>();
        foreach (var f in filas)
        {
            var propios = new List<ErrorParticipacion>();
            decimal Porcentaje(string? texto, string dato)
            {
                if (!ReglasSociedades.TryLeerPorcentaje(texto, out var v) || v < 0m || v > 100m)
                {
                    propios.Add(new ErrorParticipacion(f.Sociedad, dato, "Los porcentajes deben estar entre 0 y 100"));
                }

                return v;
            }

            var control = Porcentaje(f.Control, Control);
            var consolidacion = Porcentaje(f.Consolidacion, Consolidacion);
            var propiedad = Porcentaje(f.Propiedad, Propiedad);
            if (!Enum.TryParse<MetodoConsolidacion>(f.Metodo, out var metodo) || !Enum.IsDefined(metodo))
            {
                propios.Add(new ErrorParticipacion(f.Sociedad, Metodo, "El método es obligatorio"));
            }

            if (propios.Count == 0)
            {
                validas.Add(new Participacion(f.Sociedad, control, consolidacion, propiedad, metodo));
            }

            errores.AddRange(propios);
        }

        return (validas, errores);
    }

    /// <summary>«No se guardó la fila 2000. Los porcentajes deben estar entre 0 y 100.» (UX-13, estado de error).</summary>
    public static string MensajeFila(string sociedad, IEnumerable<ErrorParticipacion> errores) =>
        $"No se guardó la fila {sociedad}. {string.Join(". ", errores.Where(e => e.Sociedad == sociedad).Select(e => e.Motivo).Distinct())}.";

    /// <summary>Qué cambió frente al periodo anterior (UX-13): «% propiedad 80 → 90».</summary>
    public static IReadOnlyList<string> Cambios(Participacion? anterior, Participacion actual)
    {
        if (anterior is null)
        {
            return [];
        }

        var lista = new List<string>();
        void Comparar(string dato, decimal a, decimal b)
        {
            if (a != b)
            {
                lista.Add($"{dato} {ReglasSociedades.Porcentaje(a)} → {ReglasSociedades.Porcentaje(b)}");
            }
        }

        Comparar("% control", anterior.Control, actual.Control);
        Comparar("% consolidación", anterior.Consolidacion, actual.Consolidacion);
        Comparar("% propiedad", anterior.Propiedad, actual.Propiedad);
        if (anterior.Metodo != actual.Metodo)
        {
            lista.Add($"método {ReglasSociedades.Metodo(anterior.Metodo)} → {ReglasSociedades.Metodo(actual.Metodo)}");
        }

        return lista;
    }
}
