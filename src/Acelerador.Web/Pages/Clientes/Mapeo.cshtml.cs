using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-06 Mapeo de cuentas (HU-11).</summary>
public sealed class MapeoModel(ServicioMapeo servicio) : PageModel
{
    public VistaMapeo Vista { get; private set; } = null!;

    public string? Error { get; private set; }

    [TempData]
    public string? Aviso { get; set; }

    public string Enlace(string? rubro = null, bool? soloSinMapear = null)
    {
        var q = new List<string>();
        if (Vista.Sociedad is not null)
        {
            q.Add($"sociedad={Uri.EscapeDataString(Vista.Sociedad.Codigo)}");
        }

        q.Add($"rubro={Uri.EscapeDataString(rubro ?? Vista.Rubro.Codigo)}");
        if (soloSinMapear ?? Vista.SoloSinMapear)
        {
            q.Add("sinMapear=true");
        }

        return Formato.Ruta(Vista.Cliente.Id, Pasos.Mapeo) + "?" + string.Join('&', q);
    }

    public async Task<IActionResult> OnGetAsync(Guid id, string? sociedad, string? rubro, bool sinMapear, CancellationToken ct) =>
        await CargarAsync(id, sociedad, rubro, sinMapear, ct) ?? Page();

    /// <summary>Asignar al rubro o quitar del rubro las cuentas marcadas (HU-11 CA1 y CA5).</summary>
    public async Task<IActionResult> OnPostAsync(Guid id, string? sociedad, string? rubro, bool sinMapear, List<string>? cuentas, string? accion, CancellationToken ct)
    {
        var bloqueo = await CargarAsync(id, sociedad, rubro, sinMapear, ct);
        if (bloqueo is not null)
        {
            return bloqueo;
        }

        var soc = Vista.Sociedad?.Codigo ?? "";
        var marcadas = cuentas ?? [];
        var resultado = accion == "quitar"
            ? await servicio.QuitarAsync(id, soc, marcadas, ct)
            : await servicio.AsignarAsync(id, soc, Vista.Rubro.Codigo, marcadas, ct);

        if (resultado.Error is not null)
        {
            Error = resultado.Error;
            await CargarAsync(id, sociedad, rubro, sinMapear, ct);
            return Page();
        }

        var n = resultado.Guardadas;
        Aviso = accion == "quitar"
            ? $"{n} {(n == 1 ? "cuenta quitada" : "cuentas quitadas")} de su rubro."
            : $"{n} {(n == 1 ? "cuenta asignada" : "cuentas asignadas")} a {Vista.Rubro.Codigo}.";
        return Redirect(Enlace());
    }

    public async Task<IActionResult> OnPostContinuarAsync(Guid id, CancellationToken ct)
    {
        var siguiente = await servicio.ContinuarAsync(id, ct);
        return Redirect(Formato.Ruta(id, siguiente ?? Pasos.Mapeo));
    }

    private async Task<IActionResult?> CargarAsync(Guid id, string? sociedad, string? rubro, bool sinMapear, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, sociedad, rubro, sinMapear, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Cliente, Pasos.Mapeo))
        {
            return Redirect(Navegacion.Destino(vista.Cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Cliente;
        return null;
    }
}
