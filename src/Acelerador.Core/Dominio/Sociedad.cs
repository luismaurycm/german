namespace Acelerador.Core.Dominio;

/// <summary>Método de consolidación inicial de una sociedad (RN-13).</summary>
public enum MetodoConsolidacion
{
    Matriz,
    IntegracionGlobal,
    Participacion,
    FueraDelPerimetro,
}

/// <summary>
/// Sociedad del grupo que entra en la consolidación (RF-06, RF-13, RN-07). Una sociedad no SAP se carga por archivo
/// en UX-07. El % de propiedad y el método son la estructura inicial del modelo de propiedad (HU-10, RF-31).
/// </summary>
public sealed class Sociedad
{
    public string Codigo { get; set; } = "";

    public string Nombre { get; set; } = "";

    public bool EnSap { get; set; } = true;

    /// <summary>Moneda local ISO 4217 (p. ej. COP). Obligatoria (HU-09 CA4).</summary>
    public string MonedaLocal { get; set; } = "";

    /// <summary>Código de la sociedad matriz; vacío en la matriz del grupo.</summary>
    public string Matriz { get; set; } = "";

    /// <summary>Plan de cuentas propio de la sociedad (RN-05).</summary>
    public string PlanCuentas { get; set; } = "";

    public decimal PorcentajePropiedad { get; set; } = 100m;

    public MetodoConsolidacion Metodo { get; set; } = MetodoConsolidacion.IntegracionGlobal;
}

/// <summary>Participación de una sociedad en el primer periodo del modelo de propiedad (HU-10 CA4, HU-32).</summary>
public sealed record ParticipacionInicial(string Sociedad, string SociedadMatriz, decimal Porcentaje, MetodoConsolidacion Metodo);
