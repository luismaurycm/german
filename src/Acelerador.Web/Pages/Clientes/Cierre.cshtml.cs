using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-15 Estado del cierre: la matriz C04 (HU-41) y Bloquear o Desbloquear por sociedad y periodo (HU-40).</summary>
public sealed class CierreModel(ServicioCierre servicio) : PageModel
{
    public VistaC04 Vista { get; private set; } = null!;

    [TempData]
    public string? Aviso { get; set; }

    [TempData]
    public string? Fallo { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, string? periodo, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, periodo, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Contexto.Cliente, Pasos.Reportes))
        {
            return Redirect(Navegacion.Destino(vista.Contexto.Cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Contexto.Cliente;
        return Page();
    }

    public async Task<IActionResult> OnPostBloqueoAsync(Guid id, string periodo, string sociedad, bool bloquear, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, periodo, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Contexto.Cliente, Pasos.Reportes))
        {
            return Redirect(Navegacion.Destino(vista.Contexto.Cliente));
        }

        var r = await servicio.CambiarBloqueoAsync(id, periodo, sociedad, bloquear, ct);
        if (r.Exitosa)
        {
            Aviso = r.Mensaje;
        }
        else
        {
            Fallo = r.Mensaje;
        }

        return Redirect($"{Formato.Cierre(id)}?periodo={vista.Contexto.Periodo}");
    }
}
