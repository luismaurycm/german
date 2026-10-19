using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>UX-13 C02 Participaciones y métodos (HU-35): la plantilla EPM sobre el modelo de propiedad de BPC.</summary>
public sealed class ParticipacionesModel(ServicioParticipaciones servicio) : PageModel
{
    public VistaC02 Vista { get; private set; } = null!;

    /// <summary>Valores en pantalla por sociedad cuando son un borrador (copiados o con error) y no lo guardado.</summary>
    public Dictionary<string, FilaParticipacion> Borrador { get; } = [];

    public IReadOnlyList<ErrorParticipacion> Errores { get; private set; } = [];

    public string? Error { get; private set; }

    [TempData]
    public string? Aviso { get; set; }

    public bool ConError(string sociedad, string dato) => Errores.Any(e => e.Sociedad == sociedad && e.Dato == dato);

    public (string Control, string Consolidacion, string Propiedad, string Metodo) Valores(FilaC02 f)
    {
        if (Borrador.TryGetValue(f.Sociedad.Codigo, out var b))
        {
            return (b.Control ?? "", b.Consolidacion ?? "", b.Propiedad ?? "", b.Metodo ?? "");
        }

        return f.Actual is { } a
            ? (ReglasSociedades.Porcentaje(a.Control), ReglasSociedades.Porcentaje(a.Consolidacion), ReglasSociedades.Porcentaje(a.Propiedad), a.Metodo.ToString())
            : ("", "", "", "");
    }

    public async Task<IActionResult> OnGetAsync(Guid id, string? periodo, CancellationToken ct) => await CargarAsync(id, periodo, ct) ?? Page();

    public async Task<IActionResult> OnPostAsync(Guid id, string periodo, List<FilaParticipacion>? filas, string? accion, CancellationToken ct)
    {
        var r = await CargarAsync(id, periodo, ct);
        if (r is not null)
        {
            return r;
        }

        if (accion == "copiar")
        {
            // HU-35 CA2: trae las del mes anterior como borrador; se guardan con «Guardar datos».
            var anteriores = await servicio.CopiarAnteriorAsync(id, periodo, ct);
            foreach (var p in anteriores)
            {
                Borrador[p.Sociedad] = new FilaParticipacion(p.Sociedad, ReglasSociedades.Porcentaje(p.Control), ReglasSociedades.Porcentaje(p.Consolidacion), ReglasSociedades.Porcentaje(p.Propiedad), p.Metodo.ToString());
            }

            Aviso = anteriores.Count == 0
                ? "El periodo anterior no tiene participaciones para copiar."
                : $"Copiadas las participaciones vigentes al periodo anterior ({Textos.Cantidad(anteriores.Count, "sociedad", "sociedades")}). Revísalas y presiona Guardar datos.";
            return Page();
        }

        var resultado = await servicio.GuardarAsync(id, periodo, filas ?? [], ct);
        if (resultado.Error is not null || resultado.Errores.Count > 0)
        {
            Errores = resultado.Errores;
            Error = resultado.Error;
            foreach (var f in (filas ?? []).Where(f => resultado.Errores.Any(e => e.Sociedad == f.Sociedad) || resultado.Error is not null))
            {
                Borrador[f.Sociedad] = f;
            }

            await CargarAsync(id, periodo, ct);
            if (resultado.Guardadas > 0)
            {
                Aviso = $"Guardadas {Textos.Cantidad(resultado.Guardadas, "fila", "filas")} en {periodo}.";
            }

            return Page();
        }

        Aviso = $"Guardadas {Textos.Cantidad(resultado.Guardadas, "fila", "filas")} en {periodo}. Cada cambio quedó en la auditoría de BPC.";
        return Redirect($"{Formato.Participaciones(id)}?periodo={periodo}");
    }

    private async Task<IActionResult?> CargarAsync(Guid id, string? periodo, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, periodo, ct);
        if (vista is null)
        {
            return NotFound();
        }

        if (!ReglaAvance.PuedeAbrir(vista.Cliente, Pasos.Reportes))
        {
            return Redirect(Navegacion.Destino(vista.Cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = vista.Cliente;
        return null;
    }
}
