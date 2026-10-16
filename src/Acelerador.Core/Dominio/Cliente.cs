using Acelerador.Sap;

namespace Acelerador.Core.Dominio;

public enum Arquitectura
{
    SinDetectar,
    A,
    B,
}

/// <summary>Estado del cliente que se ve en UX-01. Los estados de ejecución (con error, completo) llegan con HU-13.</summary>
public enum EstadoCliente
{
    EnCurso,
    NoSoportado,
}

/// <summary>Resultado de RF-04 guardado en el cliente (HU-06 CA4).</summary>
public sealed record DeteccionArquitectura(
    Arquitectura Arquitectura,
    VersionBw Bw,
    VersionBpc Bpc,
    string? VarianteId,
    DateTimeOffset Fecha)
{
    public bool Soportada => Arquitectura == Arquitectura.A && VarianteId is not null;
}

/// <summary>Resultado de la validación ERP–BW (HU-05 CA2: fecha, usuario y mensaje).</summary>
public sealed record ValidacionErpBw(bool Exitosa, DateTimeOffset Fecha, string Usuario, string Mensaje);

/// <summary>Una implementación de consolidación en un cliente (RF-01, HU-02).</summary>
public sealed class Cliente
{
    public required Guid Id { get; init; }

    public required string Nombre { get; init; }

    public required DateTimeOffset Creado { get; init; }

    public required string CreadoPor { get; init; }

    /// <summary>Código de la pantalla del paso en que va la implementación (p. ej. UX-02).</summary>
    public string PasoActual { get; set; } = Pasos.Conexiones;

    public EstadoCliente Estado { get; set; } = EstadoCliente.EnCurso;

    public string? MotivoEstado { get; set; }

    public DeteccionArquitectura? Deteccion { get; set; }

    public ValidacionErpBw? Validacion { get; set; }

    public ISet<Modulo> Modulos { get; init; } = new HashSet<Modulo>();

    /// <summary>Avanza el paso actual sin retroceder nunca.</summary>
    public void AvanzarA(string paso)
    {
        if (Pasos.Indice(paso) > Pasos.Indice(PasoActual))
        {
            PasoActual = paso;
        }
    }
}
