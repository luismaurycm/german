using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acelerador.Core.Servicios;

/// <summary>Todo lo que muestra UX-05: las sociedades guardadas y las del ERP para el selector (o el error al leerlas).</summary>
public sealed record VistaSociedades(Cliente Cliente, IReadOnlyList<Sociedad> Sociedades, IReadOnlyList<SociedadErp> SociedadesErp, string? ErrorErp);

public sealed record ResultadoSociedadesGuardadas(bool Guardado, bool Avanzo, IReadOnlyList<ErrorSociedad> Errores, string? ErrorGuardado = null);

/// <summary>Casos de uso de HU-09 y HU-10 (UX-05).</summary>
public sealed class ServicioSociedades(
    IClienteRepository clientes,
    ISociedadRepository sociedades,
    AccesoSap acceso,
    ISapGateway sap,
    ILogger<ServicioSociedades>? logger = null)
{
    public const string MensajeNoGuardadas = "No se pudieron guardar las sociedades.";

    private readonly ILogger _log = logger ?? NullLogger<ServicioSociedades>.Instance;

    public async Task<VistaSociedades?> ObtenerAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is null)
        {
            return null;
        }

        var (erp, error) = await LeerErpAsync(clienteId, ct);
        return new VistaSociedades(cliente, await sociedades.ListarAsync(clienteId, ct), erp, error);
    }

    /// <summary>
    /// Valida y guarda todas las sociedades. Con <paramref name="continuar"/>, además exige al menos una sociedad y
    /// una matriz, y avanza al mapeo de cuentas. Nada se guarda si una fila tiene errores.
    /// </summary>
    public async Task<ResultadoSociedadesGuardadas> GuardarAsync(Guid clienteId, IReadOnlyList<FilaSociedad> filas, bool continuar, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        var bloqueo = ReglaAvance.BloqueoDeConfiguracion(cliente);
        if (bloqueo is not null)
        {
            return new ResultadoSociedadesGuardadas(false, false, [new ErrorSociedad(-1, null, bloqueo)]);
        }

        IReadOnlyList<SociedadErp> erp = [];
        if (filas.Any(f => f.EnSap))
        {
            var (leidas, error) = await LeerErpAsync(clienteId, ct);
            if (error is not null)
            {
                return new ResultadoSociedadesGuardadas(false, false, [new ErrorSociedad(-1, null, $"No se pudieron validar las sociedades contra el ERP: {error}")]);
            }

            erp = leidas;
        }

        var validacion = ReglasSociedades.Validar(filas, erp);
        var errores = validacion.Errores.ToList();
        var paraContinuar = continuar ? ReglasSociedades.BloqueoParaContinuar(validacion.Sociedades) : null;
        if (paraContinuar is not null)
        {
            errores.Add(new ErrorSociedad(-1, null, paraContinuar));
        }

        if (errores.Count > 0)
        {
            return new ResultadoSociedadesGuardadas(false, false, errores);
        }

        try
        {
            await sociedades.GuardarAsync(clienteId, validacion.Sociedades, ct);
            cliente.TieneSociedadesNoSap = validacion.Sociedades.Any(s => !s.EnSap);
            if (continuar)
            {
                cliente.AvanzarA(Pasos.Mapeo);
            }

            await clientes.ActualizarAsync(cliente, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not KeyNotFoundException)
        {
            _log.LogError(ex, "No se pudieron guardar las sociedades");
            return new ResultadoSociedadesGuardadas(false, false, [], $"{MensajeNoGuardadas} La base local respondió: {ex.Message}");
        }

        return new ResultadoSociedadesGuardadas(true, continuar, []);
    }

    /// <summary>HU-10 CA4: estructura inicial del grupo, primer periodo del modelo de propiedad (HU-32).</summary>
    public async Task<IReadOnlyList<ParticipacionInicial>> EstructuraInicialAsync(Guid clienteId, CancellationToken ct = default) =>
        ReglasSociedades.EstructuraInicial(await sociedades.ListarAsync(clienteId, ct));

    private async Task<(IReadOnlyList<SociedadErp> Sociedades, string? Error)> LeerErpAsync(Guid clienteId, CancellationToken ct)
    {
        var destino = await acceso.DestinoAsync(clienteId, SistemaSap.Erp, ct);
        if (destino.Destino is null)
        {
            return ([], destino.Error);
        }

        try
        {
            var r = await sap.LeerSociedadesAsync(destino.Destino, ct);
            return r.Exitosa ? (r.Sociedades, null) : ([], SanitizadorMensajes.Limpiar(r.Mensaje, destino.Credencial));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError("Error inesperado al leer sociedades: {Tipo}", ex.GetType().Name);
            return ([], SanitizadorMensajes.Limpiar($"Error inesperado al llamar a SAP: {ex.Message}", destino.Credencial));
        }
    }
}
