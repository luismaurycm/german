using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-04 Módulos (HU-08).</summary>
public sealed class ModulosModel(ServicioClientes clientes, ServicioModulos servicio, ILogger<ModulosModel> log) : PageModel
{
    public Cliente Cliente { get; private set; } = null!;

    public ISet<Modulo> Seleccion { get; private set; } = new HashSet<Modulo>();

    public ResumenModulos Resumen => ReglasModulos.Resumen(Seleccion);

    public string? Error { get; private set; }

    public bool ErrorAlGuardar { get; private set; }

    [TempData]
    public string? Aviso { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        if (cliente is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(cliente, Pasos.Modulos))
        {
            return Redirect(Formato.Ruta(id, cliente.Estado == EstadoCliente.NoSoportado ? Pasos.Conexiones : cliente.PasoActual));
        }

        Preparar(cliente, cliente.Modulos);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, List<Modulo>? modulos, string? accion, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        if (cliente is null)
        {
            return NotFound();
        }

        var seleccion = modulos ?? [];
        var continuar = accion == "continuar";
        ResultadoModulos resultado;
        try
        {
            resultado = await servicio.GuardarAsync(id, seleccion, continuar, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "No se pudo guardar la selección de módulos");
            Preparar(cliente, seleccion);
            ErrorAlGuardar = true;
            return Page();
        }

        if (!resultado.Guardado)
        {
            // RN-19: cliente no soportado.
            return Redirect(Formato.Ruta(id, Pasos.Conexiones));
        }

        if (resultado.Avanzo)
        {
            return Redirect(Formato.Ruta(id, Pasos.Sociedades));
        }

        if (resultado.Error is not null)
        {
            Preparar((await clientes.ObtenerAsync(id, ct))!, seleccion);
            Error = resultado.Error;
            return Page();
        }

        Aviso = "Selección guardada.";
        return Redirect(Formato.Ruta(id, Pasos.Modulos));
    }

    private void Preparar(Cliente cliente, IEnumerable<Modulo> seleccion)
    {
        Cliente = cliente;
        Seleccion = seleccion.ToHashSet();
        ViewData["Cliente"] = cliente;
    }
}
