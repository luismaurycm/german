using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

public sealed record ResumenModulos(int Modulos, int Reportes)
{
    public string Texto => $"{Modulos} {(Modulos == 1 ? "módulo" : "módulos")} · {Reportes} {(Reportes == 1 ? "reporte" : "reportes")}";
}

/// <summary>Reglas de HU-08: resumen de módulos y reportes, y al menos un módulo para continuar.</summary>
public static class ReglasModulos
{
    public const string MensajeSinModulos = "Elige al menos un módulo.";

    public static ResumenModulos Resumen(IEnumerable<Modulo> seleccion)
    {
        var fichas = seleccion.Distinct().Select(CatalogoModulos.Ficha).ToList();
        return new ResumenModulos(fichas.Count, fichas.Sum(f => f.Reportes));
    }

    public static bool PuedeContinuar(IEnumerable<Modulo> seleccion) => seleccion.Any();
}
