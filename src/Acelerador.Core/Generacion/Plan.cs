using System.Text.Json;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>Capa del plan en UX-08 (HU-13 CA1).</summary>
public enum Capa
{
    Bw,
    Bpc,
    Monedas,
}

/// <summary>Qué pasará con un objeto del plan.</summary>
public enum EstadoObjeto
{
    /// <summary>Se crea en el cliente.</summary>
    Nuevo,

    /// <summary>Ya existe en el cliente: no se recrea ni se modifica (RN-03).</summary>
    Existente,

    /// <summary>No se puede generar (p. ej. un campo Z con tipo de dato incompatible, HU-17 CA3).</summary>
    NoIncorporable,
}

/// <summary>
/// Un objeto que el acelerador crea en el cliente: dónde va, cómo se llama y la operación del paquete Z que lo crea.
/// <see cref="Grupo"/> agrupa los objetos dentro de la capa (p. ej. «Bodega financiera», «Campos Z»).
/// </summary>
public sealed record ObjetoPlan(
    string PasoId,
    Capa Capa,
    string Grupo,
    SistemaSap Sistema,
    string Tipo,
    string NombreTecnico,
    string Descripcion,
    EstadoObjeto Estado,
    OperacionSap Operacion,
    string? Nota = null);

/// <summary>Un paso de la ejecución (UX-09) con los objetos que crea.</summary>
public sealed record PasoPlan(string Id, string Nombre, IReadOnlyList<ObjetoPlan> Objetos)
{
    public int Nuevos => Objetos.Count(o => o.Estado == EstadoObjeto.Nuevo);

    /// <summary>Todo lo del paso ya existe: queda como Existente y no llama a SAP (HU-15 CA2).</summary>
    public bool SoloExistentes => Nuevos == 0 && Objetos.Any(o => o.Estado == EstadoObjeto.Existente);
}

/// <summary>Un paso anterior incompleto que impide ver el plan, con la pantalla donde se completa (HU-13 CA4).</summary>
public sealed record Faltante(string Pantalla, string Mensaje);

/// <summary>Monedas de la consolidación: moneda local por sociedad, presentación en dos monedas y tipos de tasa.</summary>
public sealed record MonedasPlan(IReadOnlyList<(string Sociedad, string Moneda)> MonedasLocales, IReadOnlyList<(string Tipo, string Aplica)> Tasas)
{
    public const string MonedaGrupo = "USD";
}

/// <summary>Plan de generación de un cliente (RF-08, HU-13).</summary>
public sealed record PlanGeneracion(
    VarianteGeneracion? Variante,
    IReadOnlyList<PasoPlan> Pasos,
    MonedasPlan Monedas,
    IReadOnlyList<Faltante> Faltantes)
{
    public IEnumerable<ObjetoPlan> Objetos => Pasos.SelectMany(p => p.Objetos);

    public int TotalNuevos => Objetos.Count(o => o.Estado == EstadoObjeto.Nuevo);

    public int TotalExistentes => Objetos.Count(o => o.Estado == EstadoObjeto.Existente);

    public bool Listo => Faltantes.Count == 0;

    public IEnumerable<ObjetoPlan> DeCapa(Capa capa) => Objetos.Where(o => o.Capa == capa);
}

/// <summary>
/// Pasos de la ejecución en el orden de UX-09. Cada sprint agrega los generadores de sus pasos; un paso sin objetos no
/// aparece en el plan ni en la ejecución.
/// </summary>
public static class PasosGeneracion
{
    public const string ObjetosBw = "BW_OBJETOS";
    public const string PlanesCuentas = "PLANES";
    public const string CentrosCosto = "CECO";
    public const string Monedas = "MONEDAS";
    public const string Jerarquia = "JERARQUIA";
    public const string ActivosFijos = "AF";
    public const string Deterioros = "DET";
    public const string NoSap = "NO_SAP";
    public const string ModeloBpc = "BPC_MODELO";
    public const string Cuadre = "CUADRE";
    public const string Importacion = "IMPORTACION";
    public const string Presupuesto = "PRESUPUESTO";
    public const string Consolidacion = "CONSOLIDACION";
    public const string Inversion = "INVERSION";
    public const string Conversion = "CONVERSION";

    public static readonly IReadOnlyList<(string Id, string Nombre)> Todos =
    [
        (ObjetosBw, "Objetos BW y carga full inicial"),
        (PlanesCuentas, "Planes de cuentas"),
        (CentrosCosto, "Centros de costo y de beneficio"),
        (Monedas, "Monedas y tasas"),
        (Jerarquia, "Jerarquía de cuentas"),
        (ActivosFijos, "Activos fijos en BW"),
        (Deterioros, "Deterioros en BW"),
        (NoSap, "Sociedades no SAP"),
        (ModeloBpc, "Modelo BPC, propiedad y scripts"),
        (Cuadre, "Validación de cuadre"),
        (Importacion, "Importación a BPC"),
        (Presupuesto, "Modelo de presupuesto"),
        (Consolidacion, "Consolidación"),
        (Inversion, "Eliminación de inversión e interés minoritario"),
        (Conversion, "Conversión a dólares y diferencia en conversión"),
    ];

    public static string Nombre(string id) => Todos.First(p => p.Id == id).Nombre;

    public static int Orden(string id)
    {
        for (var i = 0; i < Todos.Count; i++)
        {
            if (Todos[i].Id == id)
            {
                return i;
            }
        }

        return int.MaxValue;
    }
}

/// <summary>Todo lo que un generador necesita saber del cliente.</summary>
public sealed record ContextoPlan(
    Cliente Cliente,
    VarianteGeneracion Variante,
    Inventario Inventario,
    IReadOnlyList<Sociedad> Sociedades);

/// <summary>Un generador de objetos de un área (ADR-07: una implementación por variante cuando la variante importa).</summary>
public interface IGeneradorObjetos
{
    bool Aplica(ContextoPlan contexto);

    IEnumerable<ObjetoPlan> Generar(ContextoPlan contexto);
}

/// <summary>Definiciones JSON que reciben las funciones del paquete Z (contrato con INT-02 e INT-03).</summary>
public static class Definicion
{
    private static readonly JsonSerializerOptions Opciones = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Json(object definicion) => JsonSerializer.Serialize(definicion, Opciones);
}
