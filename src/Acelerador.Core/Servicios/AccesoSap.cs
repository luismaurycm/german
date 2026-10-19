using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Servicios;

/// <summary>Destino RFC listo para llamar, o el motivo por el que no se puede llamar.</summary>
public sealed record DestinoResuelto(DestinoSap? Destino, string? Error)
{
    public CredencialSap? Credencial => Destino?.Credencial;
}

/// <summary>
/// Arma el destino de un sistema con la conexión registrada en HU-03 y la credencial del almacén (ADR-04). Lo usan
/// los pasos que leen del cliente después de Conexiones: inventario, sociedades y cuentas.
/// </summary>
public sealed class AccesoSap(IConexionRepository conexiones, ICredentialStore credenciales)
{
    public async Task<DestinoResuelto> DestinoAsync(Guid clienteId, SistemaSap sistema, CancellationToken ct = default)
    {
        var c = (await conexiones.ListarAsync(clienteId, ct)).FirstOrDefault(x => x.Sistema == sistema);
        if (c is null || c.Estado != EstadoPrueba.Exitosa || c.ReferenciaCredencial is null)
        {
            return new DestinoResuelto(null, $"Prueba primero con éxito la conexión a {Textos.Sistema(sistema)} en Conexiones.");
        }

        var credencial = credenciales.Leer(c.ReferenciaCredencial);
        return credencial is null
            ? new DestinoResuelto(null, ServicioConexiones.MensajeCredencialNoDisponible)
            : new DestinoResuelto(new DestinoSap(sistema, c.Parametros, credencial), null);
    }
}
