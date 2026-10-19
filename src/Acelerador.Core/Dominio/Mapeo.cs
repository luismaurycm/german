namespace Acelerador.Core.Dominio;

/// <summary>Rubro de los reportes (p. ej. F-00-101 Efectivo y equivalentes de efectivo).</summary>
public sealed record Rubro(string Codigo, string Nombre);

/// <summary>Una cuenta del plan de cuentas asignada a un rubro (RF-07). El mapeo es por plan de cuentas (RN-05).</summary>
public sealed record AsignacionCuenta(string PlanCuentas, string Cuenta, string Rubro);

/// <summary>
/// Rubros de los estados financieros que usan los reportes R01 a R20 (prototipo F4). Es el catálogo inicial de la
/// versión 1; los reportes de consolidación (HU-38 en adelante) lo referencian por código.
/// </summary>
public static class CatalogoRubros
{
    public static readonly IReadOnlyList<Rubro> Todos =
    [
        new("F-00-101", "Efectivo y equivalentes de efectivo"),
        new("F-00-102", "Cuentas por cobrar"),
        new("F-00-103", "Propiedad, planta y equipo"),
        new("F-00-104", "Otros activos"),
        new("F-00-201", "Cuentas por pagar"),
        new("F-00-202", "Obligaciones financieras"),
        new("F-00-203", "Beneficios a empleados"),
        new("F-00-301", "Patrimonio"),
        new("F-00-401", "Ingresos de actividades ordinarias"),
        new("F-00-501", "Gastos de administración"),
        new("F-00-502", "Gastos de ventas"),
        new("F-00-503", "Gastos financieros"),
    ];

    public static Rubro? Buscar(string? codigo) => Todos.FirstOrDefault(r => r.Codigo == codigo);
}
