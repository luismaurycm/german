using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-10 Reportes (HU-48): catálogo de los módulos elegidos, Generar seleccionados y Descargar reportes (.zip).</summary>
public sealed class ReportesModel(ServicioReportes servicio) : PageModel
{
    public VistaCatalogo Vista { get; private set; } = null!;

    [TempData]
    public string? Aviso { get; set; }

    [TempData]
    public string? Fallo { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, Modulo? modulo, CancellationToken ct)
    {
        var vista = await servicio.CatalogoAsync(id, modulo, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (vista.Cliente.Estado == EstadoCliente.NoSoportado)
        {
            return Redirect(Navegacion.Destino(vista.Cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Cliente;
        return Page();
    }

    public async Task<IActionResult> OnPostGenerarAsync(Guid id, List<string>? ids, Modulo? modulo, CancellationToken ct)
    {
        var vista = await servicio.CatalogoAsync(id, modulo, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!vista.Habilitado)
        {
            return Redirect(Formato.Ruta(id, Pasos.Reportes));
        }

        if (ids is not { Count: > 0 })
        {
            Fallo = "Elige al menos un reporte para generar.";
        }
        else
        {
            var generados = await servicio.GenerarAsync(id, ids, ct);
            var errores = generados.Count(g => !g.Exitoso);
            Aviso = $"{Textos.Cantidad(generados.Count - errores, "reporte generado", "reportes generados")}.{(errores > 0 ? $" {Textos.Cantidad(errores, "reporte quedó", "reportes quedaron")} con error: revisa la causa en su estado." : "")}";
        }

        return Redirect($"{Formato.Ruta(id, Pasos.Reportes)}{(modulo is null ? "" : $"?modulo={modulo}")}");
    }

    public async Task<IActionResult> OnPostDescargarAsync(Guid id, CancellationToken ct)
    {
        var vista = await servicio.CatalogoAsync(id, null, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!vista.Habilitado)
        {
            return Redirect(Formato.Ruta(id, Pasos.Reportes));
        }

        var paquete = await servicio.DescargarAsync(id, ct);
        return File(paquete!.Zip, "application/zip", paquete.Archivo);
    }
}
