using Acelerador.Core.Dominio;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>Una cuenta del ERP con el rubro al que está asignada (o null si está sin mapear).</summary>
public sealed record CuentaMapeo(CuentaErp Cuenta, string? Rubro)
{
    public bool Mapeada => Rubro is not null;
}

/// <summary>Reglas de HU-11: cuentas del plan de la sociedad (RN-05), valor del rubro (RN-10) y cuentas sin mapear.</summary>
public static class ReglasMapeo
{
    public const string MensajeSinCuentas = "Marca al menos una cuenta.";
    public const string MensajeSinSociedades = "Registra en Sociedades al menos una sociedad que esté en SAP.";

    public static string MensajeNoGuardadas(int n) =>
        $"No se pudo guardar el mapeo. La asignación de {n} {(n == 1 ? "cuenta" : "cuentas")} no se guardó. Vuelve a asignarlas.";

    /// <summary>Cruza las cuentas del ERP con las asignaciones de su plan de cuentas.</summary>
    public static IReadOnlyList<CuentaMapeo> Cruzar(IEnumerable<CuentaErp> cuentas, IEnumerable<AsignacionCuenta> asignaciones)
    {
        var porCuenta = asignaciones.ToDictionary(a => a.Cuenta, a => a.Rubro, StringComparer.Ordinal);
        return cuentas.Select(c => new CuentaMapeo(c, porCuenta.GetValueOrDefault(c.Numero))).ToList();
    }

    /// <summary>RN-10: el valor de un rubro es la suma de las cuentas mapeadas a ese rubro.</summary>
    public static decimal ValorRubro(IEnumerable<CuentaMapeo> cuentas, string rubro) =>
        cuentas.Where(c => c.Rubro == rubro).Sum(c => c.Cuenta.Saldo);

    public static int SinMapear(IEnumerable<CuentaMapeo> cuentas) => cuentas.Count(c => !c.Mapeada);

    /// <summary>Cuentas del plan asignadas a cada rubro (el número junto al rubro en UX-06).</summary>
    public static int CuentasDelRubro(IEnumerable<CuentaMapeo> cuentas, string rubro) => cuentas.Count(c => c.Rubro == rubro);

    /// <summary>Filtro «Solo sin mapear» (HU-11 CA3).</summary>
    public static IReadOnlyList<CuentaMapeo> Filtrar(IEnumerable<CuentaMapeo> cuentas, bool soloSinMapear) =>
        cuentas.Where(c => !soloSinMapear || !c.Mapeada).ToList();
}
