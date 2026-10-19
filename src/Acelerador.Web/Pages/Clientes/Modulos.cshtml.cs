using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-04 Módulos (HU-08) con las versiones de presupuesto (HU-12).</summary>
public sealed class ModulosModel(ServicioClientes clientes, ServicioModulos servicio, ILogger<ModulosModel> log) : PageModel
{
    public Cliente Cliente { get; private set; } = null!;

    public ISet<Modulo> Seleccion { get; private set; } = new HashSet<Modulo>();

    public ResumenModulos Resumen => ReglasModulos.Resumen(Seleccion);

    public string? Error { get; private set; }

    /// <summary>Versiones en pantalla: las guardadas o el borrador que se está editando.</summary>
    public IReadOnlyList<VersionPresupuesto> Versiones { get; private set; } = [];

    public IReadOnlyList<ErrorVersion> ErroresVersiones { get; private set; } = [];

    public IEnumerable<string> ErroresDeFila(int fila) => ErroresVersiones.Where(e => e.Fila == fila).Select(e => e.Mensaje);

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
            return Redirect(Navegacion.Destino(cliente));
        }

        Preparar(cliente, cliente.Modulos, cliente.Versiones.ToList());
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, List<Modulo>? modulos, List<VersionPresupuesto>? versiones, bool conVersiones, string? accion, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(id, ct);
        if (cliente is null)
        {
            return NotFound();
        }

        var seleccion = modulos ?? [];
        var borrador = (versiones ?? []).Select(v => v with { Codigo = v.Codigo ?? "", Nombre = v.Nombre ?? "" }).ToList();

        // HU-12 CA1: agregar o quitar una versión solo cambia el borrador; se guarda con Guardar o Continuar.
        if (accion == "agregar-version")
        {
            borrador.Add(new VersionPresupuesto("", ""));
            Preparar(cliente, seleccion, borrador);
            return Page();
        }

        if (accion?.StartsWith("quitar-version-", StringComparison.Ordinal) == true
            && int.TryParse(accion["quitar-version-".Length..], out var fila) && fila >= 0 && fila < borrador.Count)
        {
            borrador.RemoveAt(fila);
            Preparar(cliente, seleccion, borrador);
            return Page();
        }

        var continuar = accion == "continuar";
        ResultadoModulos resultado;
        try
        {
            // Sin la sección de versiones en el formulario (conVersiones = false) se conservan las guardadas.
            resultado = await servicio.GuardarAsync(id, seleccion, continuar, conVersiones ? borrador : null, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "No se pudo guardar la selección de módulos");
            Preparar(cliente, seleccion, borrador);
            ErrorAlGuardar = true;
            return Page();
        }

        if (resultado.ErroresVersiones is { Count: > 0 } errores)
        {
            Preparar(cliente, seleccion, borrador);
            ErroresVersiones = errores;
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
            var actualizado = (await clientes.ObtenerAsync(id, ct))!;
            Preparar(actualizado, seleccion, actualizado.Versiones.ToList());
            Error = resultado.Error;
            return Page();
        }

        Aviso = "Selección guardada.";
        return Redirect(Formato.Ruta(id, Pasos.Modulos));
    }

    private void Preparar(Cliente cliente, IEnumerable<Modulo> seleccion, IReadOnlyList<VersionPresupuesto> versiones)
    {
        Cliente = cliente;
        Seleccion = seleccion.ToHashSet();
        Versiones = versiones;
        ViewData["Cliente"] = cliente;
    }
}
