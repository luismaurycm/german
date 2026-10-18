namespace Acelerador.Core.Dominio;

public enum ResultadoEjecucion
{
    Exitoso,
    Fallido,
    NoSoportado,
}

/// <summary>
/// Entrada del registro de ejecuciones (RNF-Aud-01): usuario, fecha y hora, sistema destino, paso,
/// objetos creados o modificados y resultado. Base de HU-54. Nunca contiene credenciales (RNF-Seg-02).
/// </summary>
public sealed record Ejecucion(
    DateTimeOffset Fecha,
    string Usuario,
    Guid ClienteId,
    string Sistema,
    string Paso,
    string Objetos,
    ResultadoEjecucion Resultado,
    string Mensaje)
{
    public long Id { get; init; }
}
