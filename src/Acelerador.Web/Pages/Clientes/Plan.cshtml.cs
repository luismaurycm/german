using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-08 Plan de generación (HU-13).</summary>
public sealed class PlanModel(ServicioPlan planes, ServicioEjecucion ejecucion) : PageModel
{
    public VistaPlan Vista { get; private set; } = null!;

    public bool Confirmar { get; private set; }

    public string? Error { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, bool confirmar, CancellationToken ct)
    {
        var r = await CargarAsync(id, ct);
        Confirmar = confirmar && Vista?.Plan.Listo == true;
        return r ?? Page();
    }

    /// <summary>«Generar» después de la confirmación (HU-13 CA3): crea la ejecución y lleva a UX-09.</summary>
    public async Task<IActionResult> OnPostGenerarAsync(Guid id, CancellationToken ct)
    {
        var r = await CargarAsync(id, ct);
        if (r is not null)
        {
            return r;
        }

        var inicio = await ejecucion.IniciarAsync(id, ct);
        if (inicio.Error is not null)
        {
            Error = inicio.Error;
            return Page();
        }

        return Redirect(Formato.Ruta(id, Pasos.Ejecucion));
    }

    private async Task<IActionResult?> CargarAsync(Guid id, CancellationToken ct)
    {
        var vista = await planes.ObtenerAsync(id, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Cliente, Pasos.Plan))
        {
            return Redirect(Navegacion.Destino(vista.Cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Cliente;
        return null;
    }
}
