using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-03 Diagnóstico e inventario (HU-07).</summary>
public sealed class DiagnosticoModel(ServicioDiagnostico servicio, ILogger<DiagnosticoModel> log) : PageModel
{
    public VistaDiagnostico Vista { get; private set; } = null!;

    public Cliente Cliente => Vista.Cliente;

    public Inventario? Inventario => Vista.Inventario;

    /// <summary>Pestaña elegida (sin JavaScript se muestran todas; con JavaScript esta queda abierta).</summary>
    public TipoInventario Pestana { get; private set; }

    public string? Error { get; private set; }

    [TempData]
    public string? Aviso { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, TipoInventario? pestana, CancellationToken ct)
    {
        Pestana = pestana ?? TipoInventario.Extractores;
        return await CargarAsync(id, ct) ?? Page();
    }

    /// <summary>Escanear o Volver a escanear (HU-07 CA3).</summary>
    public async Task<IActionResult> OnPostEscanearAsync(Guid id, CancellationToken ct)
    {
        var bloqueo = await CargarAsync(id, ct);
        if (bloqueo is not null)
        {
            return bloqueo;
        }

        var inventario = await servicio.EscanearAsync(id, ct);
        Aviso = inventario.Fallo is null ? "Escaneo completo." : null;
        return Redirect(Formato.Ruta(id, Pasos.Diagnostico) + (inventario.Fallo is null ? "" : "#escaneo"));
    }

    /// <summary>Guarda los campos Z marcados y, con «Continuar a módulos», avanza (RF-09).</summary>
    public async Task<IActionResult> OnPostCamposAsync(Guid id, List<string>? campos, string? accion, CancellationToken ct)
    {
        var bloqueo = await CargarAsync(id, ct);
        if (bloqueo is not null)
        {
            return bloqueo;
        }

        var continuar = accion == "continuar";
        ResultadoOperacion resultado;
        try
        {
            resultado = await servicio.GuardarCamposZAsync(id, campos ?? [], continuar, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "No se pudo guardar la selección de campos Z");
            Error = "No se pudo guardar la selección de campos Z. Intenta de nuevo.";
            Pestana = TipoInventario.CamposZ;
            return Page();
        }

        if (!resultado.Exitosa)
        {
            Error = resultado.Mensaje;
            Pestana = TipoInventario.CamposZ;
            await CargarAsync(id, ct);
            return Page();
        }

        if (continuar)
        {
            return Redirect(Formato.Ruta(id, Pasos.Modulos));
        }

        Aviso = resultado.Mensaje;
        return Redirect(Formato.Ruta(id, Pasos.Diagnostico) + "?pestana=CamposZ");
    }

    private async Task<IActionResult?> CargarAsync(Guid id, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Cliente, Pasos.Diagnostico))
        {
            return Redirect(Navegacion.Destino(vista.Cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Cliente;
        return null;
    }
}
