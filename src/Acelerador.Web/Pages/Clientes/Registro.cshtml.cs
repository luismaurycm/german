using Acelerador.Core.Dominio;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>Vista de registro de UX-09 (HU-54): consultar, filtrar por resultado y descargar el registro de ejecuciones.</summary>
public sealed class RegistroModel(ServicioRegistro servicio, ServicioClientes clientes, ILogger<RegistroModel> log) : PageModel
{
    public VistaRegistro? Vista { get; private set; }

    public Cliente Cliente { get; private set; } = null!;

    public ResultadoEjecucion? Filtro { get; private set; }

    public bool ErrorAlCargar { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, ResultadoEjecucion? resultado, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        if (cliente is null)
        {
            return NotFound();
        }

        Cliente = cliente;
        Filtro = resultado;
        ViewData["Cliente"] = cliente;
        try
        {
            Vista = await servicio.ObtenerAsync(id, resultado, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "No se pudo cargar el registro de ejecuciones");
            ErrorAlCargar = true;
        }

        return Page();
    }

    /// <summary>Descargar registro (HU-54 CA3): CSV con los mismos campos de la pantalla.</summary>
    public async Task<IActionResult> OnGetDescargarAsync(Guid id, CancellationToken ct)
    {
        var archivo = await servicio.ExportarAsync(id, ct);
        return archivo is null ? NotFound() : File(archivo.Contenido, "text/csv; charset=utf-8", archivo.Nombre);
    }
}
