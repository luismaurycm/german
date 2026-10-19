using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-11 Plantilla de reporte EPM con las hojas de control C01 Conciliación intercompañía (HU-38) y C03 Validación de cuadre (HU-37).</summary>
public sealed class ReporteModel(ServicioControles servicio, ServicioReportes reportes) : PageModel
{
    public static readonly IReadOnlyList<(string Id, string Nombre)> Hojas = [("C01", "C01 Conciliación IC"), ("C03", "C03 Cuadre")];

    /// <summary>R01 a R07 (HU-42 a HU-45): reporte con la estructura común.</summary>
    public VistaReporteEpm? Estado { get; private set; }

    public string Hoja { get; private set; } = "";

    public VistaC01? C01 { get; private set; }

    public VistaC03? C03 { get; private set; }

    public ContextoCierre Contexto => (C01?.Contexto ?? C03?.Contexto ?? Estado?.Contexto)!;

    public string? Error => C01?.Error ?? C03?.Error ?? Estado?.Error;

    public async Task<IActionResult> OnGetAsync(Guid id, string codigo, string? periodo, string? moneda, string? sociedad, string? filiales, string? rubro, CancellationToken ct)
    {
        Hoja = codigo.ToUpperInvariant();
        Cliente? cliente;
        switch (Hoja)
        {
            case ['R', ..]:
                Estado = await reportes.ConsultarAsync(id, Hoja, sociedad, filiales is null ? null : filiales == "Sí", moneda, periodo, rubro, ct);
                cliente = Estado?.Contexto.Cliente;
                break;
            case "C01":
                C01 = await servicio.C01Async(id, periodo, moneda, ct);
                cliente = C01?.Contexto.Cliente;
                break;
            case "C03":
                C03 = await servicio.C03Async(id, periodo, ct);
                cliente = C03?.Contexto.Cliente;
                break;
            default:
                return NotFound();
        }

        if (cliente is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(cliente, Pasos.Reportes))
        {
            return Redirect(Navegacion.Destino(cliente));
        }

        ViewData["Cliente"] = cliente;
        return Page();
    }
}
