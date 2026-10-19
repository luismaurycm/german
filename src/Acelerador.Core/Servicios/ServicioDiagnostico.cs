using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Servicios;

/// <summary>Todo lo que muestra UX-03 para un cliente.</summary>
public sealed record VistaDiagnostico(Cliente Cliente, Inventario? Inventario)
{
    public string? Bloqueo => ReglasInventario.BloqueoParaContinuar(Inventario);

    public bool PuedeContinuar => Bloqueo is null;
}

/// <summary>
/// Orquesta UX-03 Diagnóstico e inventario (HU-07): escanea extractores, InfoProviders, modelos BPC y campos Z
/// con el usuario técnico, marca la bodega existente (RN-03) y guarda los campos Z que se incorporan (RF-09).
/// Cada paso del escaneo queda en el registro de ejecuciones (RNF-Aud-01).
/// </summary>
public sealed class ServicioDiagnostico(
    IClienteRepository clientes,
    IInventarioRepository inventarios,
    AccesoSap acceso,
    IRegistroEjecuciones registro,
    ISapGateway sap,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string PrefijoPaso = "Inventario: ";

    public async Task<VistaDiagnostico?> ObtenerAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        return cliente is null ? null : new VistaDiagnostico(cliente, await inventarios.ObtenerAsync(clienteId, ct));
    }

    /// <summary>Escanea paso por paso; si uno falla, guarda el paso y el mensaje y conserva el último inventario bueno.</summary>
    public async Task<Inventario> EscanearAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        var inventario = await inventarios.ObtenerAsync(clienteId, ct) ?? new Inventario { ClienteId = clienteId };
        var leidos = new Dictionary<TipoInventario, IReadOnlyList<ObjetoInventario>>();

        foreach (var paso in ReglasInventario.Pasos)
        {
            var destino = await acceso.DestinoAsync(clienteId, paso.Sistema, ct);
            ResultadoInventario resultado;
            if (destino.Destino is null)
            {
                resultado = new ResultadoInventario(false, destino.Error!, []);
            }
            else
            {
                try
                {
                    resultado = await sap.LeerInventarioAsync(destino.Destino, paso.Tipo, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    resultado = new ResultadoInventario(false, $"Error inesperado al llamar a SAP: {ex.Message}", []);
                }
            }

            var mensaje = SanitizadorMensajes.Limpiar(resultado.Mensaje, destino.Credencial);
            await registro.RegistrarAsync(new Ejecucion(
                reloj.GetUtcNow(), usuario.Nombre, cliente.Id, Textos.Sistema(paso.Sistema), PrefijoPaso + paso.Nombre,
                ServicioConexiones.SinObjetos, resultado.Exitosa ? ResultadoEjecucion.Exitoso : ResultadoEjecucion.Fallido, mensaje), ct);

            if (!resultado.Exitosa)
            {
                inventario.Fallo = new FalloEscaneo(paso.Tipo, mensaje, reloj.GetUtcNow());
                await inventarios.GuardarAsync(inventario, ct);
                return inventario;
            }

            leidos[paso.Tipo] = resultado.Objetos;
        }

        inventario.CamposZIncorporados = ReglasInventario.ConservarSeleccion(inventario.CamposZIncorporados, leidos[TipoInventario.CamposZ]);
        inventario.Objetos = leidos;
        inventario.Fecha = reloj.GetUtcNow();
        inventario.Usuario = usuario.Nombre;
        inventario.Fallo = null;
        await inventarios.GuardarAsync(inventario, ct);
        return inventario;
    }

    /// <summary>
    /// Guarda los campos Z que se incorporan a la bodega (solo los que existen en el inventario). Con
    /// <paramref name="continuar"/>, exige un escaneo completo y avanza el cliente a Módulos.
    /// </summary>
    public async Task<ResultadoOperacion> GuardarCamposZAsync(Guid clienteId, IEnumerable<string> campos, bool continuar, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        var bloqueoCliente = ReglaAvance.BloqueoDeConfiguracion(cliente);
        if (bloqueoCliente is not null)
        {
            return new ResultadoOperacion(false, bloqueoCliente);
        }

        var inventario = await inventarios.ObtenerAsync(clienteId, ct);
        var bloqueo = ReglasInventario.BloqueoParaContinuar(inventario);
        if (inventario is null || !inventario.Escaneado)
        {
            return new ResultadoOperacion(false, bloqueo!);
        }

        inventario.CamposZIncorporados = ReglasInventario.ConservarSeleccion(campos, inventario.Lista(TipoInventario.CamposZ));
        await inventarios.GuardarAsync(inventario, ct);

        if (!continuar)
        {
            return new ResultadoOperacion(true, "Selección de campos Z guardada.");
        }

        if (bloqueo is not null)
        {
            return new ResultadoOperacion(false, bloqueo);
        }

        cliente.AvanzarA(Pasos.Modulos);
        await clientes.ActualizarAsync(cliente, ct);
        return new ResultadoOperacion(true, "Inventario listo.");
    }
}
