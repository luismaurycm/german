using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;

namespace Acelerador.Core.Servicios;

public sealed record ResultadoModulos(bool Guardado, bool Avanzo, string? Error, IReadOnlyList<ErrorVersion>? ErroresVersiones = null);

/// <summary>Casos de uso de HU-08 y HU-12 (UX-04).</summary>
public sealed class ServicioModulos(IClienteRepository clientes)
{
    /// <summary>
    /// Guarda la selección. Con <paramref name="continuar"/>, además exige al menos un módulo (HU-08 CA3)
    /// y avanza el cliente a Sociedades. Un cliente no soportado no se configura (RN-19). Con Presupuesto marcado,
    /// <paramref name="versiones"/> reemplaza las versiones del cliente si son válidas (HU-12); si no lo son, no se guarda nada.
    /// </summary>
    public async Task<ResultadoModulos> GuardarAsync(
        Guid clienteId,
        IEnumerable<Modulo> seleccion,
        bool continuar,
        IReadOnlyList<VersionPresupuesto>? versiones = null,
        CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");

        var bloqueo = ReglaAvance.BloqueoDeConfiguracion(cliente);
        if (bloqueo is not null)
        {
            return new ResultadoModulos(false, false, bloqueo);
        }

        var lista = seleccion.Distinct().ToList();
        if (versiones is not null && lista.Contains(Modulo.Presupuesto))
        {
            var normalizadas = ReglasVersiones.Normalizar(versiones);
            var errores = ReglasVersiones.Validar(normalizadas);
            if (errores.Count > 0)
            {
                return new ResultadoModulos(false, false, null, errores);
            }

            cliente.Versiones = [.. normalizadas];
        }

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
