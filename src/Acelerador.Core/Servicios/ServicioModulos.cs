using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;

namespace Acelerador.Core.Servicios;

public sealed record ResultadoModulos(bool Guardado, bool Avanzo, string? Error);

/// <summary>Casos de uso de HU-08 (UX-04).</summary>
public sealed class ServicioModulos(IClienteRepository clientes)
{
    /// <summary>
    /// Guarda la selección. Con <paramref name="continuar"/>, además exige al menos un módulo (HU-08 CA3)
    /// y avanza el cliente a Sociedades. Un cliente no soportado no se configura (RN-19).
    /// </summary>
    public async Task<ResultadoModulos> GuardarAsync(Guid clienteId, IEnumerable<Modulo> seleccion, bool continuar, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");

        var bloqueo = ReglaAvance.BloqueoDeConfiguracion(cliente);
        if (bloqueo is not null)
        {
            return new ResultadoModulos(false, false, bloqueo);
        }

        var lista = seleccion.Distinct().ToList();
        cliente.Modulos.Clear();
        foreach (var m in lista)
        {
            cliente.Modulos.Add(m);
        }

        var avanzo = false;
        string? error = null;
        if (continuar)
        {
            if (ReglasModulos.PuedeContinuar(lista))
            {
                cliente.AvanzarA(Pasos.Sociedades);
                avanzo = true;
            }
            else
            {
                error = ReglasModulos.MensajeSinModulos;
            }
        }

        await clientes.ActualizarAsync(cliente, ct);
        return new ResultadoModulos(true, avanzo, error);
    }
}
