using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-09 Ejecución y registro (HU-14, HU-15, HU-54).</summary>
public sealed class EjecucionModel(ServicioEjecucion servicio) : PageModel
{
    public VistaEjecucion Vista { get; private set; } = null!;

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => await CargarAsync(id, ct) ?? Page();

    /// <summary>Estado en JSON para que la página se actualice mientras la ejecución avanza.</summary>
    public async Task<IActionResult> OnGetEstadoAsync(Guid id, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, ct);
        var e = vista?.Ejecucion;
        return new JsonResult(new
        {
            estado = e?.Estado.ToString(),
            pasos = e?.Pasos.Select(p => p.Estado.ToString()),
        });
    }

    /// <summary>«Reintentar desde este paso» (HU-14 CA2 y CA3).</summary>
    public async Task<IActionResult> OnPostReintentarAsync(Guid id, CancellationToken ct)
    {
        var r = await CargarAsync(id, ct);
        if (r is not null)
        {
            return r;
        }

        var resultado = await servicio.ReintentarAsync(id, ct);
        if (resultado.Error is not null)
        {
            Error = resultado.Error;
            return Page();
        }

        return Redirect(Formato.Ruta(id, Pasos.Ejecucion));
    }

    private async Task<IActionResult?> CargarAsync(Guid id, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Cliente, Pasos.Ejecucion))
        {
            return Redirect(Navegacion.Destino(vista.Cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Cliente;
        return null;
    }
}
