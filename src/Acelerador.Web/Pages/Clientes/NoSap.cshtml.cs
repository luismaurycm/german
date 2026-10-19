using System.Text;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-07 Carga de sociedades no SAP (HU-25).</summary>
[RequestSizeLimit(LectorArchivoNoSap.TamanoMaximo + 1024 * 1024)]
public sealed class NoSapModel(ServicioNoSap servicio) : PageModel
{
    public const int FilasVistaPrevia = 20;

    public VistaNoSap Vista { get; private set; } = null!;

    [TempData]
    public string? Aviso { get; set; }

    [TempData]
    public bool AvisoExitoso { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct) => await CargarAsync(id, ct) ?? Page();

    /// <summary>Plantilla del formato provisional (pregunta 5 del DAF).</summary>
    public IActionResult OnGetPlantilla(Guid id, string sociedad) =>
        File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(LectorArchivoNoSap.Plantilla(sociedad))).ToArray(),
            "text/csv; charset=utf-8", $"plantilla-no-sap-{sociedad}.csv");

    /// <summary>Subir y validar (HU-25 CA1, CA2, CA4). Siempre redirige para que la vista previa se vea con un GET.</summary>
    public async Task<IActionResult> OnPostSubirAsync(Guid id, string sociedad, IFormFile? archivo, CancellationToken ct)
    {
        var r = await CargarAsync(id, ct);
        if (r is not null)
        {
            return r;
        }

        if (archivo is null || archivo.Length == 0)
        {
            Avisar($"Elige el archivo de {sociedad}.", false);
        }
        else if (archivo.Length > LectorArchivoNoSap.TamanoMaximo)
        {
            Avisar($"El archivo pasa de {LectorArchivoNoSap.TamanoMaximo / 1024 / 1024} MB; divídelo por periodo.", false);
        }
        else
        {
            using var memoria = new MemoryStream();
            await archivo.CopyToAsync(memoria, ct);
            var resultado = await servicio.ValidarAsync(id, sociedad, Path.GetFileName(archivo.FileName), memoria.ToArray(), ct);
            Avisar(resultado.Valido
                ? $"{resultado.Nombre}: {Textos.Cantidad(resultado.Filas.Count, "fila", "filas")} sin errores. Revisa la vista previa y confirma la carga."
                : $"{resultado.Nombre}: {resultado.TotalErrores} {(resultado.TotalErrores == 1 ? "error" : "errores")} en el archivo.", resultado.Valido);
        }

        return Redirect($"{Formato.Ruta(id, Pasos.NoSap)}#soc-{sociedad}");
    }

    /// <summary>Confirmar carga (HU-25 CA3): carga a BW todo o nada.</summary>
    public async Task<IActionResult> OnPostConfirmarAsync(Guid id, string sociedad, CancellationToken ct)
    {
        var r = await CargarAsync(id, ct);
        if (r is not null)
        {
            return r;
        }

        var resultado = await servicio.ConfirmarAsync(id, sociedad, ct);
        Avisar(resultado.Mensaje, resultado.Exitosa);
        return Redirect($"{Formato.Ruta(id, Pasos.NoSap)}#soc-{sociedad}");
    }

    public async Task<IActionResult> OnPostDescartarAsync(Guid id, string sociedad, CancellationToken ct)
    {
        var r = await CargarAsync(id, ct);
        if (r is not null)
        {
            return r;
        }

        servicio.Descartar(id, sociedad);
        return Redirect($"{Formato.Ruta(id, Pasos.NoSap)}#soc-{sociedad}");
    }

    public async Task<IActionResult> OnPostContinuarAsync(Guid id, CancellationToken ct)
    {
        var siguiente = await servicio.ContinuarAsync(id, ct);
        if (siguiente is null)
        {
            Avisar("Confirma la carga de todas las sociedades no SAP antes de continuar.", false);
            return Redirect(Formato.Ruta(id, Pasos.NoSap));
        }

        return Redirect(Formato.Ruta(id, siguiente));
    }

    private void Avisar(string mensaje, bool exitoso)
    {
        Aviso = mensaje;
        AvisoExitoso = exitoso;
    }

    private async Task<IActionResult?> CargarAsync(Guid id, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Cliente, Pasos.NoSap))
        {
            return Redirect(Navegacion.Destino(vista.Cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Cliente;
        return null;
    }
}
