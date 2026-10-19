using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>Pasos que aún no se construyen (UX-07 a UX-10). Mantiene la navegación del flujo.</summary>
public sealed class PasoModel(ServicioClientes clientes) : PageModel
{
    public Cliente Cliente { get; private set; } = null!;

    public Paso Paso { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(Guid id, string codigo, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        var paso = Pasos.Buscar(codigo);
        if (cliente is null || paso is null)
        {
            return NotFound();
        }

        if (paso.Construido)
        {
            return Redirect(Formato.Ruta(id, paso.Codigo));
        }

        if (!ReglaAvance.PuedeAbrir(cliente, paso.Codigo))
        {
            return Redirect(Navegacion.Destino(cliente));
        }

        Cliente = cliente;
        Paso = paso;
        ViewData["Cliente"] = cliente;
        return Page();
    }
}
