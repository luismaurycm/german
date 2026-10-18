using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages;

public enum EstadoPantalla
{
    Normal,
    Vacio,
    SinResultados,
    Error,
}

/// <summary>UX-01 Clientes (HU-02, HU-06 CA4, HU-57 CA3).</summary>
public sealed class IndexModel(ServicioClientes servicio, ILogger<IndexModel> log) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "q")]
    public string? Busqueda { get; set; }

    [BindProperty(SupportsGet = true, Name = "nuevo")]
    public bool MostrarDialogo { get; set; }

    [BindProperty]
    public string? Nombre { get; set; }

    public string? ErrorNombre { get; private set; }

    public ListaClientes? Lista { get; private set; }

    public EstadoPantalla Estado { get; private set; }

    public async Task OnGetAsync(CancellationToken ct) => await CargarAsync(ct);

    public async Task<IActionResult> OnPostCrearAsync(CancellationToken ct)
    {
        ResultadoCreacion resultado;
        try
        {
            resultado = await servicio.CrearAsync(Nombre, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "No se pudo crear el cliente");
            resultado = new ResultadoCreacion(null, "No se pudo crear el cliente. Inténtalo de nuevo.");
        }

        if (resultado.Cliente is null)
        {
            MostrarDialogo = true;
            ErrorNombre = resultado.Error;
            await CargarAsync(ct);
            return Page();
        }

        // HU-02 CA3: al crear el cliente se pasa a UX-02 Conexiones.
        return Redirect(Formato.Ruta(resultado.Cliente.Id, Core.Dominio.Pasos.Conexiones));
    }

    private async Task CargarAsync(CancellationToken ct)
    {
        try
        {
            Lista = await servicio.ListarAsync(Busqueda, ct);
            Estado = Lista.Total == 0 ? EstadoPantalla.Vacio
                : Lista.Filas.Count == 0 ? EstadoPantalla.SinResultados
                : EstadoPantalla.Normal;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "No se pudo cargar la lista de clientes");
            Estado = EstadoPantalla.Error;
        }
    }
}
