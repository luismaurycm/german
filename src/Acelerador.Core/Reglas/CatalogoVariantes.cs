using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>Tipo de InfoProvider que genera una variante de BW (RNF-Comp-05: aDSO en BW/4HANA).</summary>
public enum TipoInfoProvider
{
    InfoCubo,
    Adso,
}

/// <summary>Una variante de generación: la combinación de versiones que el acelerador sabe generar (ADR-07).</summary>
public sealed record VarianteGeneracion(
    string Id,
    string Nombre,
    ProductoBw Bw,
    ProductoBpc Bpc,
    ModeloBpc Modelo,
    TipoInfoProvider InfoProvider);

/// <summary>
/// Catálogo de variantes de la versión 1: solo arquitectura A con BPC modelo estándar (ADR-07).
/// Agregar una versión es agregar una entrada y sus generadores, sin tocar las existentes (RNF-Comp-04).
/// </summary>
public sealed class CatalogoVariantes
{
    public static readonly VarianteGeneracion Bw75Bpc101 =
        new("A-BW75-BPC101-STD", "BW 7.5 + BPC 10.1 modelo estándar", ProductoBw.Bw75, ProductoBpc.Bpc101, ModeloBpc.Estandar, TipoInfoProvider.InfoCubo);

    public static readonly VarianteGeneracion Bw4HanaBpc2021 =
        new("A-BW4H-BPC2021-STD", "BW/4HANA + BPC 2021 modelo estándar", ProductoBw.Bw4Hana, ProductoBpc.Bpc2021, ModeloBpc.Estandar, TipoInfoProvider.Adso);

    public static readonly CatalogoVariantes VersionUno = new([Bw75Bpc101, Bw4HanaBpc2021]);

    public CatalogoVariantes(IReadOnlyList<VarianteGeneracion> variantes)
    {
        Variantes = variantes;
    }

    public IReadOnlyList<VarianteGeneracion> Variantes { get; }

    public VarianteGeneracion? Buscar(VersionBw bw, VersionBpc bpc) =>
        Variantes.FirstOrDefault(v => v.Bw == bw.Producto && v.Bpc == bpc.Producto && v.Modelo == bpc.Modelo);

    public VarianteGeneracion? Buscar(string? id) => Variantes.FirstOrDefault(v => v.Id == id);
}
