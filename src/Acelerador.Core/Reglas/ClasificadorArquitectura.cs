using Acelerador.Core.Dominio;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>Resultado de clasificar el paisaje del cliente (RF-04).</summary>
public sealed record Clasificacion(Arquitectura Arquitectura, VarianteGeneracion? Variante, string Titulo, string Explicacion)
{
    public bool Soportada => Arquitectura == Arquitectura.A && Variante is not null;
}

/// <summary>
/// HU-06 y HU-57. Arquitectura B = ERP y BW en el mismo sistema (S/4HANA con BW y BPC embebidos);
/// arquitectura A = sistemas separados. En A, la variante sale del catálogo; sin variante,
/// «Versión no soportada aún». La B se identifica pero no se configura (RN-19).
/// </summary>
public static class ClasificadorArquitectura
{
    public const string TituloNoSoportadaAun = "Versión no soportada aún";
    public const string TituloArquitecturaB = "Arquitectura B no soportada en la versión 1";
    public const string TituloVarianteDisponible = "Variante disponible";

    public const string ExplicacionArquitecturaB =
        "ERP, BW y BPC están integrados en el mismo sistema (S/4HANA con BW y BPC embebidos). " +
        "La versión 1 del acelerador solo genera objetos en la arquitectura A: este cliente se identifica, " +
        "pero no se configura ni se genera nada en sus sistemas.";

    public static Clasificacion Clasificar(PaisajeSap paisaje, CatalogoVariantes catalogo) =>
        string.Equals(paisaje.SidErp, paisaje.SidBw, StringComparison.OrdinalIgnoreCase)
            ? ArquitecturaB
            : ArquitecturaA(paisaje.Bw, paisaje.Bpc, catalogo);

    /// <summary>Describe lo guardado en el cliente con el catálogo vigente.</summary>
    public static Clasificacion Describir(DeteccionArquitectura deteccion, CatalogoVariantes catalogo) =>
        deteccion.Arquitectura == Arquitectura.B ? ArquitecturaB : ArquitecturaA(deteccion.Bw, deteccion.Bpc, catalogo);

    private static readonly Clasificacion ArquitecturaB = new(Arquitectura.B, null, TituloArquitecturaB, ExplicacionArquitecturaB);

    private static Clasificacion ArquitecturaA(VersionBw bw, VersionBpc bpc, CatalogoVariantes catalogo)
    {
        var variante = catalogo.Buscar(bw, bpc);
        if (variante is null)
        {
            return new Clasificacion(
                Arquitectura.A,
                null,
                TituloNoSoportadaAun,
                $"{Textos.Version(bw)} con {Textos.Version(bpc)} no tiene variante de generación en esta versión del acelerador. " +
                "Se agregará como una variante nueva, sin cambiar las existentes.");
        }

        return new Clasificacion(
            Arquitectura.A,
            variante,
            TituloVarianteDisponible,
            $"ERP, {Textos.Version(bw)} y {Textos.Version(bpc)} en sistemas separados. " +
            $"Los InfoProviders se generan como {(variante.InfoProvider == TipoInfoProvider.Adso ? "aDSO" : "InfoCubos")}.");
    }
}
