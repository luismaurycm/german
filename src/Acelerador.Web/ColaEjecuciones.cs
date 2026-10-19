using System.Threading.Channels;
using Acelerador.Core.Puertos;
using Acelerador.Core.Servicios;

namespace Acelerador.Web;

/// <summary>
/// Ejecuta el plan de generación en segundo plano (HU-14), una ejecución a la vez, para que UX-09 muestre el avance
/// mientras SAP responde. Si el acelerador se cierra a medias, al arrancar el paso en curso queda con error y se reintenta.
/// </summary>
public sealed class ColaEjecuciones(IServiceScopeFactory alcances, ILogger<ColaEjecuciones> log) : BackgroundService, IColaEjecuciones
{
    private readonly Channel<(Guid Cliente, Guid Ejecucion)> _canal = Channel.CreateUnbounded<(Guid, Guid)>();

    public void Encolar(Guid clienteId, Guid ejecucionId) => _canal.Writer.TryWrite((clienteId, ejecucionId));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var (cliente, ejecucion) in _canal.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var alcance = alcances.CreateScope();
                await alcance.ServiceProvider.GetRequiredService<ServicioEjecucion>().EjecutarAsync(cliente, ejecucion, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "La ejecución {Ejecucion} terminó con un error inesperado", ejecucion);
            }
        }
    }
}
