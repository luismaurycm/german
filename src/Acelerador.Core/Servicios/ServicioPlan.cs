using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Puertos;

namespace Acelerador.Core.Servicios;

/// <summary>Todo lo que muestra UX-08: el plan y la última ejecución, si la hay.</summary>
public sealed record VistaPlan(Cliente Cliente, PlanGeneracion Plan, EjecucionPlan? UltimaEjecucion);

/// <summary>Arma el plan de generación de un cliente con su configuración guardada (HU-13).</summary>
public sealed class ServicioPlan(
    IClienteRepository clientes,
    IInventarioRepository inventarios,
    ISociedadRepository sociedades,
    IMapeoRepository mapeo,
    IEjecucionPlanRepository ejecuciones,
    GeneradorPlan generador)
{
    public async Task<VistaPlan?> ObtenerAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is null)
        {
            return null;
        }

        return new VistaPlan(cliente, await ArmarAsync(cliente, ct), await ejecuciones.ObtenerUltimaAsync(clienteId, ct));
    }

    public async Task<PlanGeneracion> ArmarAsync(Cliente cliente, CancellationToken ct = default)
    {
        var inventario = await inventarios.ObtenerAsync(cliente.Id, ct);
        var lista = await sociedades.ListarAsync(cliente.Id, ct);
        var mapeadas = 0;
        foreach (var plan in lista.Where(s => s.EnSap).Select(s => s.PlanCuentas).Distinct())
        {
            mapeadas += (await mapeo.ListarAsync(cliente.Id, plan, ct)).Count;
        }

        return generador.Armar(cliente, inventario, lista, mapeadas);
    }
}
