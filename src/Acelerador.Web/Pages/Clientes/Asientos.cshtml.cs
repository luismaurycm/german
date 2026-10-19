using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-14 Asientos de ajuste (HU-39): encabezado, líneas, totales con Cuadrado o Descuadrado y asientos del periodo.</summary>
public sealed class AsientosModel(ServicioAsientos servicio) : PageModel
{
    public const int LineasIniciales = 2;

    public VistaAsientos Vista { get; private set; } = null!;

    public List<LineaAsientoTexto> Lineas { get; private set; } = [];

    public AsientoValidado Validado { get; private set; } = null!;

    public string? Descripcion { get; private set; }

    /// <summary>Si se intentó guardar: muestra los errores de las celdas y el «no cuadra».</summary>
    public bool Intentado { get; private set; }

    public string? Error { get; private set; }

    [TempData]
    public string? Aviso { get; set; }

    public bool ConError(int linea, string dato) => Intentado && Validado.Errores.Any(e => e.Linea == linea && e.Dato == dato);

    public async Task<IActionResult> OnGetAsync(Guid id, string? periodo, string? moneda, CancellationToken ct)
    {
        var r = await CargarAsync(id, periodo, moneda, ct);
        if (r is not null)
        {
            return r;
        }

        Lineas = [.. Enumerable.Repeat(new LineaAsientoTexto(null, null, null, null, null, null), LineasIniciales)];
        Validado = ServicioAsientos.Validar(Vista, Lineas);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, string periodo, string moneda, string? descripcion, List<LineaAsientoTexto>? lineas, string? accion, CancellationToken ct)
    {
        var r = await CargarAsync(id, periodo, moneda, ct);
        if (r is not null)
        {
            return r;
        }

        Lineas = lineas ?? [];
        Descripcion = descripcion;
        switch (accion)
        {
            case "agregar":
                Lineas.Add(new LineaAsientoTexto(null, null, null, null, null, null));
                Validado = ServicioAsientos.Validar(Vista, Lineas);
                return Page();
            case "guardar":
                var resultado = await servicio.GuardarAsync(id, Vista.Contexto.Periodo, Vista.Moneda, descripcion, Lineas, ct);
                if (resultado.Numero is not null)
                {
                    Aviso = $"Asiento {resultado.Numero} guardado en {Vista.Contexto.Periodo} ({Vista.Moneda}).";
                    return Redirect($"{Formato.Asientos(id)}?periodo={Vista.Contexto.Periodo}&moneda={Vista.Moneda}");
                }

                Intentado = true;
                Validado = resultado.Validado;
                Error = resultado.Error;
                await CargarAsync(id, periodo, moneda, ct);
                return Page();
            default:
                Validado = ServicioAsientos.Validar(Vista, Lineas);
                return Page();
        }
    }

    private async Task<IActionResult?> CargarAsync(Guid id, string? periodo, string? moneda, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, periodo, moneda, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Contexto.Cliente, Pasos.Reportes))
        {
            return Redirect(Navegacion.Destino(vista.Contexto.Cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Contexto.Cliente;
        return null;
    }
}
