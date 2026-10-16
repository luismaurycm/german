namespace Acelerador.Sap.Nco;

/// <summary>Una fila de la tabla CVERS (componente de software y su release) que devuelve Z_ACC_SISTEMA_INFO.</summary>
public sealed record ComponenteSap(string Componente, string Release);

/// <summary>
/// Traduce los componentes de software instalados a la versión de BW y BPC. Es lógica pura para
/// que el conector NCo la use sin probarla contra SAP.
/// </summary>
/// <remarks>
/// La correspondencia de BW es estándar (SAP_BW 750 = BW 7.5; DW4CORE = BW/4HANA).
/// La de BPC queda POR CONFIRMAR en la prueba HU-01 (ver <see cref="ComponentesBpcConocidos"/>);
/// el tipo de modelo (estándar o embebido) lo devuelve Z_ACC_BPC_INFO por ambiente.
/// </remarks>
public static class InterpretadorComponentes
{
    private static readonly Dictionary<string, string> ReleasesBw4Hana = new()
    {
        ["100"] = "1.0",
        ["200"] = "2.0",
        ["300"] = "2021",
        ["400"] = "2023",
    };

    public static VersionBw InterpretarBw(IEnumerable<ComponenteSap> componentes)
    {
        var lista = componentes.ToList();

        var bw4 = lista.FirstOrDefault(c => Es(c, "DW4CORE"));
        if (bw4 is not null)
        {
            return new VersionBw(ProductoBw.Bw4Hana, ReleasesBw4Hana.GetValueOrDefault(bw4.Release.Trim(), bw4.Release.Trim()));
        }

        var bw = lista.FirstOrDefault(c => Es(c, "SAP_BW"));
        if (bw is null)
        {
            return new VersionBw(ProductoBw.NoReconocido, "sin componente BW");
        }

        var release = FormatearRelease(bw.Release);
        return new VersionBw(release == "7.5" ? ProductoBw.Bw75 : ProductoBw.NoReconocido, release);
    }

    /// <summary>
    /// Componentes de BPC conocidos: (componente, release) → producto y release visible.
    /// POR CONFIRMAR en la prueba HU-01 (paso 6 del guion): CPMBPC 810 para BPC 10.1 en NetWeaver
    /// y el componente de BPC 2021 en BW/4HANA, que se agrega aquí cuando se lea del ambiente real.
    /// Mientras no esté confirmado, BPC 2021 se reporta como no reconocido por el conector real.
    /// </summary>
    internal static readonly Dictionary<(string Componente, string Release), (ProductoBpc Producto, string Release)> ComponentesBpcConocidos = new()
    {
        [("CPMBPC", "810")] = (ProductoBpc.Bpc101, "10.1"),
    };

    public static VersionBpc InterpretarBpc(IEnumerable<ComponenteSap> componentes, ModeloBpc modelo)
    {
        foreach (var c in componentes)
        {
            var clave = (c.Componente.Trim().ToUpperInvariant(), c.Release.Trim());
            if (ComponentesBpcConocidos.TryGetValue(clave, out var conocido))
            {
                return new VersionBpc(conocido.Producto, conocido.Release, modelo);
            }

            if (clave.Item1.StartsWith("CPMBPC", StringComparison.Ordinal) || clave.Item1.StartsWith("BPC", StringComparison.Ordinal))
            {
                return new VersionBpc(ProductoBpc.NoReconocido, $"{clave.Item1} {clave.Item2}", modelo);
            }
        }

        return new VersionBpc(ProductoBpc.NoReconocido, "sin componente BPC", modelo);
    }

    /// <summary>«750» → «7.5»; «740» → «7.4»; «731» → «7.31».</summary>
    internal static string FormatearRelease(string release)
    {
        var r = release.Trim();
        if (r.Length != 3 || !r.All(char.IsDigit))
        {
            return r;
        }

        var menor = r[1..].TrimEnd('0');
        return menor.Length == 0 ? $"{r[0]}.0" : $"{r[0]}.{menor}";
    }

    private static bool Es(ComponenteSap c, string nombre) =>
        string.Equals(c.Componente.Trim(), nombre, StringComparison.OrdinalIgnoreCase);
}
