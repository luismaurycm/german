using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acelerador.Core.Servicios;

/// <summary>Todo lo que muestra UX-09: la última ejecución y las entradas recientes del registro.</summary>
public sealed record VistaEjecucion(Cliente Cliente, EjecucionPlan? Ejecucion, IReadOnlyList<Ejecucion> Registro);

public sealed record ResultadoInicio(Guid? EjecucionId, string? Error);

/// <summary>Tiempo de espera de cada llamada RFC (arquitectura 8.3: 120 s, un reintento solo en errores de comunicación).</summary>
public sealed record OpcionesEjecucion(TimeSpan TiempoEsperaRfc)
{
    public static readonly OpcionesEjecucion Predeterminadas = new(TimeSpan.FromSeconds(120));
}

/// <summary>
/// Orquestador de pasos (HU-14, arquitectura sección 6): ejecuta el plan en orden, escribe en el cliente solo por el
/// paquete Z, registra cada objeto (RNF-Aud-01), se detiene en el primer error y reanuda desde ese paso.
/// </summary>
public sealed class ServicioEjecucion(
    IClienteRepository clientes,
    IEjecucionPlanRepository ejecuciones,
    ServicioPlan planes,
    AccesoSap acceso,
    ISapGateway sap,
    IRegistroEjecuciones registro,
    IColaEjecuciones cola,
    IUsuarioActual usuario,
    TimeProvider reloj,
    OpcionesEjecucion? opciones = null,
    IProveedorPlantillas? plantillas = null,
    ILogger<ServicioEjecucion>? logger = null)
{
    public const string PrefijoComunicacion = "Error de comunicación: ";

    private readonly TimeSpan _espera = (opciones ?? OpcionesEjecucion.Predeterminadas).TiempoEsperaRfc;

    public const int EntradasRecientes = 8;

    private readonly ILogger _log = logger ?? NullLogger<ServicioEjecucion>.Instance;

    public async Task<VistaEjecucion?> ObtenerAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is null)
        {
            return null;
        }

        var recientes = (await registro.ListarAsync(clienteId, ct)).Take(EntradasRecientes).ToList();
        return new VistaEjecucion(cliente, await ejecuciones.ObtenerUltimaAsync(clienteId, ct), recientes);
    }

    /// <summary>«Generar» en UX-08: crea la ejecución con los pasos del plan y la pone en la cola.</summary>
    public async Task<ResultadoInicio> IniciarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        var anterior = await ejecuciones.ObtenerUltimaAsync(clienteId, ct);
        if (anterior is { Estado: EstadoEjecucion.EnCurso })
        {
            return new ResultadoInicio(null, ReglasEjecucion.MensajeEnCurso);
        }

        var plan = await planes.ArmarAsync(cliente, ct);
        if (!plan.Listo || plan.Variante is null)
        {
            return new ResultadoInicio(null, plan.Faltantes.FirstOrDefault()?.Mensaje ?? "El plan no está listo.");
        }

        var ejecucion = new EjecucionPlan
        {
            Id = Guid.NewGuid(),
            ClienteId = clienteId,
            VarianteId = plan.Variante.Id,
            Inicio = reloj.GetUtcNow(),
            Usuario = usuario.Nombre,
            Pasos = plan.Pasos.Select(p => new PasoEjecucion { Id = p.Id, Nombre = p.Nombre, Objetos = p.Objetos }).ToList(),
        };
        await ejecuciones.GuardarAsync(ejecucion, ct);

        cliente.AvanzarA(Pasos.Ejecucion);
        cliente.Estado = EstadoCliente.EnCurso;
        await clientes.ActualizarAsync(cliente, ct);
        cola.Encolar(clienteId, ejecucion.Id);
        return new ResultadoInicio(ejecucion.Id, null);
    }

    /// <summary>«Reintentar desde este paso» (HU-14 CA2 y CA3): no vuelve a ejecutar los pasos completos.</summary>
    public async Task<ResultadoInicio> ReintentarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var ejecucion = await ejecuciones.ObtenerUltimaAsync(clienteId, ct);
        if (ejecucion is null || !ReglasEjecucion.PrepararReintento(ejecucion))
        {
            return new ResultadoInicio(null, "No hay un paso con error para reintentar.");
        }

        await ejecuciones.GuardarAsync(ejecucion, ct);
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is not null)
        {
            cliente.Estado = EstadoCliente.EnCurso;
            await clientes.ActualizarAsync(cliente, ct);
        }

        cola.Encolar(clienteId, ejecucion.Id);
        return new ResultadoInicio(ejecucion.Id, null);
    }

    /// <summary>Ejecuta los pasos pendientes en orden y se detiene en el primero que falla. Lo llama la cola.</summary>
    public async Task EjecutarAsync(Guid clienteId, Guid ejecucionId, CancellationToken ct = default)
    {
        var ejecucion = await ejecuciones.ObtenerAsync(ejecucionId, ct);
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (ejecucion is null || cliente is null || ejecucion.ClienteId != clienteId)
        {
            return;
        }

        foreach (var paso in ejecucion.Pasos.Where(p => !p.Terminado))
        {
            if (!await EjecutarPasoAsync(cliente, ejecucion, paso, ct))
            {
                cliente.Estado = EstadoCliente.ConError;
                await clientes.ActualizarAsync(cliente, ct);
                return;
            }
        }

        ejecucion.Fin = reloj.GetUtcNow();
        await ejecuciones.GuardarAsync(ejecucion, ct);
        cliente.Estado = EstadoCliente.Completo;
        cliente.AvanzarA(Pasos.Reportes);
        await clientes.ActualizarAsync(cliente, ct);
    }

    /// <summary>Al arrancar: una ejecución que quedó a medias (se cerró el acelerador) queda con error en su paso en curso.</summary>
    public async Task<int> MarcarInterrumpidasAsync(CancellationToken ct = default)
    {
        var abiertas = await ejecuciones.ListarAbiertasAsync(ct);
        var n = 0;
        foreach (var e in abiertas.Where(e => e.Estado == EstadoEjecucion.EnCurso))
        {
            var paso = e.Pasos.FirstOrDefault(p => p.Estado == EstadoPaso.EnCurso) ?? e.Pasos.First(p => !p.Terminado);
            paso.Estado = EstadoPaso.Error;
            paso.Mensaje = ReglasEjecucion.MensajeInterrumpida;
            paso.PantallaCorrige = Pasos.Plan;
            paso.Fin = reloj.GetUtcNow();
            await ejecuciones.GuardarAsync(e, ct);
            var cliente = await clientes.ObtenerAsync(e.ClienteId, ct);
            if (cliente is not null)
            {
                cliente.Estado = EstadoCliente.ConError;
                await clientes.ActualizarAsync(cliente, ct);
            }

            n++;
        }

        return n;
    }

    private async Task<bool> EjecutarPasoAsync(Cliente cliente, EjecucionPlan ejecucion, PasoEjecucion paso, CancellationToken ct)
    {
        paso.Estado = EstadoPaso.EnCurso;
        paso.Inicio = reloj.GetUtcNow();
        paso.Creados = 0;
        paso.Detalles = [];
        paso.Avisos = [];
        paso.Existentes = paso.Objetos.Count(o => o.Estado == EstadoObjeto.Existente);
        await ejecuciones.GuardarAsync(ejecucion, ct);

        var nuevos = paso.Objetos.Where(o => o.Estado == EstadoObjeto.Nuevo).ToList();
        if (nuevos.Count == 0)
        {
            // RN-03: todo existe; el paso no llama a SAP.
            paso.Estado = paso.Existentes > 0 ? EstadoPaso.Existente : EstadoPaso.Completo;
            paso.Fin = reloj.GetUtcNow();
            await RegistrarAsync(cliente.Id, "—", paso.Nombre, "Ninguno (existentes, no se modifican)", ResultadoEjecucion.Exitoso,
                $"{paso.Existentes} objetos ya existían; no se crea ni se modifica nada (RN-03).", ct);
            await ejecuciones.GuardarAsync(ejecucion, ct);
            return true;
        }

        foreach (var objeto in nuevos)
        {
            var destino = await acceso.DestinoAsync(cliente.Id, objeto.Sistema, ct);
            ResultadoOperacionSap resultado;
            if (destino.Destino is null)
            {
                resultado = new ResultadoOperacionSap(false, destino.Error!);
            }
            else
            {
                var operacion = ConContenido(objeto.Operacion);
                resultado = await LlamarAsync(destino.Destino, operacion, ct);
                if (resultado.ErrorDeComunicacion)
                {
                    // Un solo reintento automático, y solo en errores de comunicación (arquitectura 8.3).
                    await RegistrarAsync(cliente.Id, Textos.Sistema(objeto.Sistema), paso.Nombre, objeto.NombreTecnico, ResultadoEjecucion.Fallido,
                        SanitizadorMensajes.Limpiar($"Intento 1: {resultado.Mensaje} Se reintenta una vez.", destino.Credencial), ct);
                    resultado = await LlamarAsync(destino.Destino, operacion, ct);
                    if (!resultado.Exitosa)
                    {
                        resultado = resultado with { Mensaje = $"Intento 2: {resultado.Mensaje}" };
                    }
                }
            }

            var mensaje = SanitizadorMensajes.Limpiar(resultado.Mensaje, destino.Credencial);
            var sistema = Textos.Sistema(objeto.Sistema);
            if (!resultado.Exitosa)
            {
                paso.Estado = EstadoPaso.Error;
                paso.Mensaje = mensaje;
                paso.PantallaCorrige = ReglasEjecucion.PantallaQueCorrige(mensaje);
                paso.Fin = reloj.GetUtcNow();
                await RegistrarAsync(cliente.Id, sistema, paso.Nombre, objeto.NombreTecnico, ResultadoEjecucion.Fallido, mensaje, ct);
                await ejecuciones.GuardarAsync(ejecucion, ct);
                return false;
            }

            if (resultado.Resultado == ResultadoEscritura.Existente)
            {
                paso.Existentes++;
            }
            else
            {
                paso.Creados++;
            }

            if (resultado.Cantidad is not null)
            {
                paso.Detalles.Add(mensaje);
            }

            if (resultado.Aviso is { } aviso)
            {
                paso.Avisos.Add(SanitizadorMensajes.Limpiar(aviso, destino.Credencial));
                mensaje = $"{mensaje} Aviso: {paso.Avisos[^1]}";
            }

            // HU-15 CA3: cada objeto queda en el registro con su nombre técnico.
            await RegistrarAsync(cliente.Id, sistema, paso.Nombre, $"{objeto.Tipo} {objeto.NombreTecnico}", ResultadoEjecucion.Exitoso, mensaje, ct);
        }

        paso.Estado = EstadoPaso.Completo;
        paso.Fin = reloj.GetUtcNow();
        await ejecuciones.GuardarAsync(ejecucion, ct);
        return true;
    }

    /// <summary>
    /// Las plantillas EPM (C02 y siguientes) se generan al ejecutar, con el proveedor de plantillas (Open XML), y viajan en
    /// la definición en base64 hacia Z_ACC_BPC_SUBIR_PLANTILLA.
    /// </summary>
    private OperacionSap ConContenido(OperacionSap operacion)
    {
        if (operacion.TipoObjeto != "PLANTILLA" || plantillas is null)
        {
            return operacion;
        }

        var nodo = System.Text.Json.Nodes.JsonNode.Parse(operacion.Definicion)!.AsObject();
        var bytes = plantillas.Generar(nodo["plantilla"]!.GetValue<string>(), nodo["parametros"]!.ToJsonString());
        nodo["contenido"] = Convert.ToBase64String(bytes);
        return operacion with { Definicion = nodo.ToJsonString() };
    }

    /// <summary>Una llamada RFC con su tiempo de espera; el tiempo agotado y las excepciones de red son errores de comunicación.</summary>
    private async Task<ResultadoOperacionSap> LlamarAsync(DestinoSap destino, OperacionSap operacion, CancellationToken ct)
    {
        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(_espera);
        try
        {
            return await sap.EjecutarAsync(destino, operacion, limite.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ResultadoOperacionSap(false,
                $"{PrefijoComunicacion}{operacion.Funcion} no respondió en {_espera.TotalSeconds:0} s (tiempo de espera agotado).", ErrorDeComunicacion: true);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError("Error inesperado al escribir en SAP: {Tipo}", ex.GetType().Name);
            return new ResultadoOperacionSap(false, $"{PrefijoComunicacion}{ex.Message}", ErrorDeComunicacion: ex is IOException or System.Net.Sockets.SocketException);
        }
    }

    private Task RegistrarAsync(Guid clienteId, string sistema, string paso, string objetos, ResultadoEjecucion resultado, string mensaje, CancellationToken ct) =>
        registro.RegistrarAsync(new Ejecucion(reloj.GetUtcNow(), usuario.Nombre, clienteId, sistema, paso, objetos, resultado, mensaje), ct);
}
