using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>Modelo de una tarjeta de sistema en UX-02.</summary>
public sealed record TarjetaConexion(
    Conexion Conexion,
    DatosConexion? Borrador,
    IReadOnlyDictionary<string, string> Errores,
    bool AlmacenPersistente)
{
    public string Id => Conexion.Sistema.ToString().ToLowerInvariant();

    public string Valor(string campo) => campo switch
    {
        ReglasConexion.CampoHost => Borrador?.Host ?? Conexion.Host,
        ReglasConexion.CampoMandante => Borrador?.Mandante ?? Conexion.Mandante,
        _ => Borrador?.UsuarioTecnico ?? Conexion.UsuarioTecnico,
    };

    public string? Error(string campo) => Errores.GetValueOrDefault(campo);
}

/// <summary>UX-02 Conexiones: HU-03, HU-04, HU-05, HU-06 y HU-57.</summary>
public sealed class ConexionesModel(ServicioConexiones servicio, ICredentialStore almacen, ILogger<ConexionesModel> log) : PageModel
{
    public VistaConexiones Vista { get; private set; } = null!;

    public SistemaSap? SistemaConErrores { get; private set; }

    public IReadOnlyDictionary<string, string> Errores { get; private set; } = new Dictionary<string, string>();

    public DatosConexion? Borrador { get; private set; }

    public IReadOnlyList<string>? PendientesAlContinuar { get; private set; }

    [TempData]
    public string? Aviso { get; set; }

    [TempData]
    public bool AvisoExitoso { get; set; }

    [TempData]
    public string? ErrorDeteccion { get; set; }

    public TarjetaConexion Tarjeta(SistemaSap sistema) => new(
        Vista[sistema],
        SistemaConErrores == sistema ? Borrador : null,
        SistemaConErrores == sistema ? Errores : new Dictionary<string, string>(),
        almacen.EsPersistente);

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) =>
        await CargarAsync(id, ct) ? Page() : NotFound();

    /// <remarks>
    /// La credencial se lee del formulario y no como parámetro del handler: ASP.NET Core escribe los
    /// argumentos de los handlers en su registro de nivel Trace, y la clave no puede llegar a ningún registro (RNF-Seg-02).
    /// </remarks>
    public async Task<IActionResult> OnPostProbarAsync(
        Guid id, SistemaSap sistema, string? host, string? mandante, string? usuarioTecnico, CancellationToken ct)
    {
        var credencial = Request.Form["credencial"].ToString();
        if (await servicio.ObtenerAsync(id, ct) is null)
        {
            return NotFound();
        }

        var datos = new DatosConexion(host, mandante, usuarioTecnico, credencial);
        ResultadoProbar resultado;
        try
        {
            resultado = await servicio.GuardarYProbarAsync(id, sistema, datos, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Sin el mensaje de la excepción: podría venir de una capa que no sanitiza (RNF-Seg-02).
            log.LogError("No se pudo guardar o probar la conexión a {Sistema}: {Tipo}", sistema, ex.GetType().Name);
            Aviso = $"No se pudo guardar la conexión a {Textos.Sistema(sistema)}. Inténtalo de nuevo.";
            AvisoExitoso = false;
            return Redirect($"{Formato.Ruta(id, Pasos.Conexiones)}#tarjeta-{sistema.ToString().ToLowerInvariant()}");
        }

        if (!resultado.Valido)
        {
            await CargarAsync(id, ct);
            SistemaConErrores = sistema;
            Errores = resultado.Errores;
            Borrador = datos with { CredencialNueva = null };
            return Page();
        }

        var c = resultado.Conexion!;
        AvisoExitoso = c.Estado == EstadoPrueba.Exitosa;
        Aviso = AvisoExitoso
            ? $"Conexión exitosa a {Textos.Sistema(sistema)}."
            : $"Falló la conexión a {Textos.Sistema(sistema)}: {c.Mensaje}";
        ErrorDeteccion = resultado.ErrorDeteccion;
        return Redirect($"{Formato.Ruta(id, Pasos.Conexiones)}#tarjeta-{sistema.ToString().ToLowerInvariant()}");
    }

    public async Task<IActionResult> OnPostValidarAsync(Guid id, CancellationToken ct)
    {
        if (await servicio.ObtenerAsync(id, ct) is null)
        {
            return NotFound();
        }

        var r = await servicio.ValidarErpBwAsync(id, ct);
        AvisoExitoso = r.Exitosa;
        Aviso = r.Exitosa ? "Conexión ERP–BW validada." : $"La validación ERP–BW falló: {r.Mensaje}";
        return Redirect($"{Formato.Ruta(id, Pasos.Conexiones)}#erp-bw");
    }

    public async Task<IActionResult> OnPostDetectarAsync(Guid id, CancellationToken ct)
    {
        if (await servicio.ObtenerAsync(id, ct) is null)
        {
            return NotFound();
        }

        var r = await servicio.DetectarAsync(id, ct);
        AvisoExitoso = r.Exitosa;
        Aviso = r.Exitosa ? $"Arquitectura identificada: {r.Mensaje}." : null;
        ErrorDeteccion = r.Exitosa ? null : r.Mensaje;
        return Redirect($"{Formato.Ruta(id, Pasos.Conexiones)}#arquitectura");
    }

    public async Task<IActionResult> OnPostContinuarAsync(Guid id, CancellationToken ct)
    {
        if (await servicio.ObtenerAsync(id, ct) is null)
        {
            return NotFound();
        }

        var pendientes = await servicio.ContinuarADiagnosticoAsync(id, ct);
        if (pendientes.Count == 0)
        {
            return Redirect(Formato.Ruta(id, Pasos.Diagnostico));
        }

        await CargarAsync(id, ct);
        PendientesAlContinuar = pendientes;
        return Page();
    }

    private async Task<bool> CargarAsync(Guid id, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, ct);
        if (vista is null)
        {
            return false;
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Cliente;
        return true;
    }
}
