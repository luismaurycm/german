using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acelerador.Core.Servicios;

/// <summary>Un rubro con el número de cuentas del plan que tiene asignadas.</summary>
public sealed record RubroMapeo(Rubro Rubro, int Cuentas);

/// <summary>Todo lo que muestra UX-06 para una sociedad y un rubro.</summary>
public sealed record VistaMapeo(
    Cliente Cliente,
    IReadOnlyList<Sociedad> Sociedades,
    Sociedad? Sociedad,
    Rubro Rubro,
    bool SoloSinMapear,
    IReadOnlyList<RubroMapeo> Rubros,
    IReadOnlyList<CuentaMapeo> Cuentas,
    int TotalCuentas,
    int SinMapear,
    decimal ValorRubro,
    string Moneda,
    string Periodo,
    string? Error)
{
    public int CuentasDelRubro => Rubros.FirstOrDefault(r => r.Rubro == Rubro)?.Cuentas ?? 0;

    /// <summary>Estado vacío: el plan todavía no tiene ninguna cuenta mapeada.</summary>
    public bool SinMapeos => Error is null && TotalCuentas > 0 && SinMapear == TotalCuentas;
}

public sealed record ResultadoMapeo(int Guardadas, string? Error);

/// <summary>
/// Casos de uso de HU-11 (UX-06): las cuentas se leen del ERP por la conexión de HU-03, filtradas por el plan de
/// cuentas de la sociedad (RN-05); el rubro muestra la suma de sus cuentas (RN-10).
/// </summary>
public sealed class ServicioMapeo(
    IClienteRepository clientes,
    ISociedadRepository sociedades,
    IMapeoRepository mapeo,
    AccesoSap acceso,
    ISapGateway sap,
    ILogger<ServicioMapeo>? logger = null)
{
    private readonly ILogger _log = logger ?? NullLogger<ServicioMapeo>.Instance;

    public async Task<VistaMapeo?> ObtenerAsync(Guid clienteId, string? sociedad, string? rubro, bool soloSinMapear, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is null)
        {
            return null;
        }

        var enSap = (await sociedades.ListarAsync(clienteId, ct)).Where(s => s.EnSap).ToList();
        var soc = enSap.FirstOrDefault(s => s.Codigo == sociedad) ?? enSap.FirstOrDefault();
        var elegido = CatalogoRubros.Buscar(rubro) ?? CatalogoRubros.Todos[0];
        VistaMapeo Vacia(string? error) => new(cliente, enSap, soc, elegido, soloSinMapear,
            CatalogoRubros.Todos.Select(r => new RubroMapeo(r, 0)).ToList(), [], 0, 0, 0m, "", "", error);

        if (soc is null)
        {
            return Vacia(ReglasMapeo.MensajeSinSociedades);
        }

        var leidas = await LeerCuentasAsync(clienteId, soc, ct);
        if (leidas.Error is not null)
        {
            return Vacia(leidas.Error);
        }

        var todas = ReglasMapeo.Cruzar(leidas.Resultado!.Cuentas, await mapeo.ListarAsync(clienteId, soc.PlanCuentas, ct));
        var rubros = CatalogoRubros.Todos.Select(r => new RubroMapeo(r, ReglasMapeo.CuentasDelRubro(todas, r.Codigo))).ToList();
        return new VistaMapeo(
            cliente, enSap, soc, elegido, soloSinMapear, rubros,
            ReglasMapeo.Filtrar(todas, soloSinMapear),
            todas.Count,
            ReglasMapeo.SinMapear(todas),
            ReglasMapeo.ValorRubro(todas, elegido.Codigo),
            leidas.Resultado.Moneda,
            leidas.Resultado.Periodo,
            null);
    }

    /// <summary>Asigna las cuentas marcadas al rubro. Si el guardado falla, informa cuántas asignaciones no se guardaron (CA5).</summary>
    public Task<ResultadoMapeo> AsignarAsync(Guid clienteId, string sociedad, string rubro, IReadOnlyList<string> cuentas, CancellationToken ct = default) =>
        CatalogoRubros.Buscar(rubro) is null
            ? Task.FromResult(new ResultadoMapeo(0, $"El rubro {rubro} no existe."))
            : GuardarAsync(clienteId, sociedad, rubro, cuentas, ct);

    /// <summary>Quita las cuentas marcadas de su rubro: vuelven a quedar sin mapear.</summary>
    public Task<ResultadoMapeo> QuitarAsync(Guid clienteId, string sociedad, IReadOnlyList<string> cuentas, CancellationToken ct = default) =>
        GuardarAsync(clienteId, sociedad, null, cuentas, ct);

    /// <summary>Continúa a UX-07 si hay sociedades no SAP; si no, al plan de generación. Devuelve el paso o null.</summary>
    public async Task<string?> ContinuarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        if (ReglaAvance.BloqueoDeConfiguracion(cliente) is not null || !ReglaAvance.PuedeAbrir(cliente, Pasos.Mapeo))
        {
            return null;
        }

        var siguiente = Pasos.DespuesDelMapeo(cliente);
        cliente.AvanzarA(siguiente);
        await clientes.ActualizarAsync(cliente, ct);
        return siguiente;
    }

    private async Task<ResultadoMapeo> GuardarAsync(Guid clienteId, string sociedad, string? rubro, IReadOnlyList<string> cuentas, CancellationToken ct)
    {
        var marcadas = cuentas.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.Ordinal).ToList();
        if (marcadas.Count == 0)
        {
            return new ResultadoMapeo(0, ReglasMapeo.MensajeSinCuentas);
        }

        var soc = (await sociedades.ListarAsync(clienteId, ct)).FirstOrDefault(s => s.EnSap && s.Codigo == sociedad);
        if (soc is null)
        {
            return new ResultadoMapeo(0, $"La sociedad {sociedad} no está registrada en SAP.");
        }

        // Solo se mapean cuentas que existen en el plan de la sociedad en el ERP (RN-05).
        var leidas = await LeerCuentasAsync(clienteId, soc, ct);
        if (leidas.Error is not null)
        {
            return new ResultadoMapeo(0, leidas.Error);
        }

        var delPlan = leidas.Resultado!.Cuentas.Select(c => c.Numero).ToHashSet(StringComparer.Ordinal);
        var ajenas = marcadas.Where(c => !delPlan.Contains(c)).ToList();
        if (ajenas.Count > 0)
        {
            return new ResultadoMapeo(0, $"La cuenta {ajenas[0]} no está en el plan de cuentas {soc.PlanCuentas}.");
        }

        try
        {
            await mapeo.GuardarAsync(clienteId, soc.PlanCuentas, marcadas, rubro, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "No se pudo guardar el mapeo de cuentas");
            return new ResultadoMapeo(0, ReglasMapeo.MensajeNoGuardadas(marcadas.Count));
        }

        return new ResultadoMapeo(marcadas.Count, null);
    }

    private async Task<(ResultadoCuentas? Resultado, string? Error)> LeerCuentasAsync(Guid clienteId, Sociedad soc, CancellationToken ct)
    {
        var destino = await acceso.DestinoAsync(clienteId, SistemaSap.Erp, ct);
        if (destino.Destino is null)
        {
            return (null, destino.Error);
        }

        ResultadoCuentas r;
        try
        {
            r = await sap.LeerCuentasAsync(destino.Destino, soc.Codigo, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError("Error inesperado al leer cuentas: {Tipo}", ex.GetType().Name);
            return (null, SanitizadorMensajes.Limpiar($"Error inesperado al llamar a SAP: {ex.Message}", destino.Credencial));
        }

        if (!r.Exitosa)
        {
            return (null, SanitizadorMensajes.Limpiar(r.Mensaje, destino.Credencial));
        }

        // RN-05: las cuentas son las del plan de la sociedad; si el ERP tiene otro plan, se corrige en Sociedades.
        return r.PlanCuentas == soc.PlanCuentas
            ? (r, null)
            : (null, $"El plan de cuentas de {soc.Codigo} en el ERP es {r.PlanCuentas}, no {soc.PlanCuentas}. Corrígelo en Sociedades.");
    }
}
