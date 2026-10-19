using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-05 Sociedades del grupo (HU-09) con % de propiedad y método inicial (HU-10).</summary>
public sealed class SociedadesModel(ServicioSociedades servicio) : PageModel
{
    public const string NuevaNoSap = "no-sap";

    public Cliente Cliente { get; private set; } = null!;

    public IReadOnlyList<SociedadErp> SociedadesErp { get; private set; } = [];

    public string? ErrorErp { get; private set; }

    /// <summary>Filas en pantalla: las guardadas o el borrador que se está editando.</summary>
    public List<FilaSociedad> Filas { get; private set; } = [];

    public IReadOnlyList<ErrorSociedad> Errores { get; private set; } = [];

    public string? ErrorGuardado { get; private set; }

    [TempData]
    public string? Aviso { get; set; }

    /// <summary>Sociedades del ERP que todavía no están en la tabla (opciones de «Agregar sociedad»).</summary>
    public IEnumerable<SociedadErp> Disponibles =>
        SociedadesErp.Where(s => !Filas.Any(f => string.Equals((f.Codigo ?? "").Trim(), s.Codigo, StringComparison.OrdinalIgnoreCase)));

    public bool Error(int fila, string campo) => Errores.Any(e => e.Fila == fila && e.Campo == campo);

    public IEnumerable<string> ErroresDeFila(int fila) => Errores.Where(e => e.Fila == fila).Select(e => e.Mensaje).Distinct();

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var vista = await CargarAsync(id, ct);
        if (vista is IActionResult r)
        {
            return r;
        }

        Filas = ((VistaSociedades)vista).Sociedades.Select(ReglasSociedades.AFila).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, List<FilaSociedad>? filas, string? nueva, string? accion, CancellationToken ct)
    {
        var vista = await CargarAsync(id, ct);
        if (vista is IActionResult r)
        {
            return r;
        }

        Filas = filas ?? [];

        if (accion == "agregar")
        {
            Filas.Add(ReglasSociedades.Nueva(SociedadesErp.FirstOrDefault(s => s.Codigo == nueva), Filas));
            return Page();
        }

        if (accion?.StartsWith("quitar-", StringComparison.Ordinal) == true
            && int.TryParse(accion["quitar-".Length..], out var fila) && fila >= 0 && fila < Filas.Count)
        {
            Filas.RemoveAt(fila);
            return Page();
        }

        var continuar = accion == "continuar";
        var resultado = await servicio.GuardarAsync(id, Filas, continuar, ct);
        if (!resultado.Guardado)
        {
            Errores = resultado.Errores;
            ErrorGuardado = resultado.ErrorGuardado;
            return Page();
        }

        if (resultado.Avanzo)
        {
            return Redirect(Formato.Ruta(id, Pasos.Mapeo));
        }

        Aviso = "Sociedades guardadas.";
        return Redirect(Formato.Ruta(id, Pasos.Sociedades));
    }

    private async Task<object> CargarAsync(Guid id, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Cliente, Pasos.Sociedades))
        {
            return Redirect(Navegacion.Destino(vista.Cliente));
        }

        Cliente = vista.Cliente;
        SociedadesErp = vista.SociedadesErp;
        ErrorErp = vista.ErrorErp;
        ViewData["Cliente"] = vista.Cliente;
        return vista;
    }
}
