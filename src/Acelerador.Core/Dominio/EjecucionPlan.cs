using Acelerador.Core.Generacion;

namespace Acelerador.Core.Dominio;

public enum EstadoPaso
{
    Pendiente,
    EnCurso,
    Completo,

    /// <summary>Todo lo del paso ya existía: no se llamó a SAP (RN-03, HU-15 CA2).</summary>
    Existente,
    Error,
}

public enum EstadoEjecucion
{
    EnCurso,
    ConError,
    Completa,
}

/// <summary>Un paso de la ejecución con su estado, duración, objetos y, si falló, la causa y la pantalla que la corrige.</summary>
public sealed class PasoEjecucion
{
    public required string Id { get; init; }

    public required string Nombre { get; init; }

    public required IReadOnlyList<ObjetoPlan> Objetos { get; init; }

    public EstadoPaso Estado { get; set; } = EstadoPaso.Pendiente;

    public DateTimeOffset? Inicio { get; set; }

    public DateTimeOffset? Fin { get; set; }

    public int Creados { get; set; }

    public int Existentes { get; set; }

    public string? Mensaje { get; set; }

    /// <summary>Código de la pantalla que corrige la causa del error (HU-14 CA2).</summary>
    public string? PantallaCorrige { get; set; }

    public TimeSpan? Duracion => Inicio is null || Fin is null ? null : Fin - Inicio;

    public bool Terminado => Estado is EstadoPaso.Completo or EstadoPaso.Existente;
}

/// <summary>Una ejecución del plan de generación (HU-14). Guarda el plan con el que empezó para reintentar sin recalcularlo.</summary>
public sealed class EjecucionPlan
{
    public required Guid Id { get; init; }

    public required Guid ClienteId { get; init; }

    public required string VarianteId { get; init; }

    public required DateTimeOffset Inicio { get; init; }

    public required string Usuario { get; init; }

    public DateTimeOffset? Fin { get; set; }

    public List<PasoEjecucion> Pasos { get; init; } = [];

    public EstadoEjecucion Estado =>
        Pasos.Any(p => p.Estado == EstadoPaso.Error) ? EstadoEjecucion.ConError
        : Pasos.All(p => p.Terminado) ? EstadoEjecucion.Completa
        : EstadoEjecucion.EnCurso;

    public int Creados => Pasos.Sum(p => p.Creados);

    public int Existentes => Pasos.Sum(p => p.Existentes);

    public PasoEjecucion? PasoConError => Pasos.FirstOrDefault(p => p.Estado == EstadoPaso.Error);

    /// <summary>Número (desde 1) del paso en curso o del siguiente pendiente.</summary>
    public int PasoActual => Pasos.FindIndex(p => !p.Terminado) + 1;
}
