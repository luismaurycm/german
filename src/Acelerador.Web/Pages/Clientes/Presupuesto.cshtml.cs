using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Acelerador.Web.Pages.Clientes;

/// <summary>Una fila de P05 tal como llega del formulario.</summary>
public sealed class FilaPresupuestoForm
{
    public string Cuenta { get; set; } = "";

    public string Centro { get; set; } = "";

    public List<string?> Meses { get; set; } = [];
}

/// <summary>UX-12 Captura de presupuesto (HU-50, HU-51): P05 por cuenta, centro y mes en la versión y el año elegidos.</summary>
public sealed class PresupuestoModel(ServicioPresupuesto servicio) : PageModel
{
    public VistaPresupuesto Vista { get; private set; } = null!;

    /// <summary>Textos de las celdas cuando la página vuelve con errores (las celdas inválidas conservan lo escrito).</summary>
    public Dictionary<(int Fila, int Mes), string> Borrador { get; } = [];

    public IReadOnlyList<ErrorCeldaPresupuesto> Errores { get; private set; } = [];

    public string? Error { get; private set; }

    [TempData]
    public string? Aviso { get; set; }

    public bool ConError(int fila, int mes) => Errores.Any(e => e.Fila == fila && e.Mes == mes);

    public string Celda(int fila, int mes) =>
        Borrador.TryGetValue((fila, mes), out var t) ? t
        : Vista.Filas[fila - 1].Meses[mes - 1] is var v && v != 0m ? ReglaCuadre.Formato(v) : "";

    public async Task<IActionResult> OnGetAsync(Guid id, string? sociedad, string? version, int? anio, CancellationToken ct) =>
        await CargarAsync(id, sociedad, version, anio, ct) ?? Page();

    public async Task<IActionResult> OnPostAsync(Guid id, string sociedad, string version, int anio, List<FilaPresupuestoForm>? filas, CancellationToken ct)
    {
        var r = await CargarAsync(id, sociedad, version, anio, ct);
        if (r is not null)
        {
            return r;
        }

        var enviadas = filas ?? [];
        var resultado = await servicio.GuardarAsync(id, sociedad, version, anio,
            enviadas.Select(f => new FilaPresupuestoTexto(f.Cuenta, f.Centro, f.Meses)).ToList(), ct);
        if (resultado.Error is null && resultado.Errores.Count == 0)
        {
            Aviso = $"Presupuesto guardado: {Vista.Version.Nombre} {Vista.Anio} de {Vista.Sociedad} en el modelo de planeación de BPC.";
            return Redirect($"{Formato.Presupuesto(id)}?sociedad={Vista.Sociedad}&version={Vista.Version.Codigo}&anio={Vista.Anio}");
        }

        await CargarAsync(id, sociedad, version, anio, ct);
        Errores = resultado.Errores;
        Error = resultado.Error;
        foreach (var e in resultado.Errores)
        {
            var f = enviadas[e.Fila - 1];
            Borrador[(e.Fila, e.Mes)] = e.Mes <= f.Meses.Count ? f.Meses[e.Mes - 1] ?? "" : "";
        }

        if (resultado.Guardadas > 0)
        {
            Aviso = $"Se guardaron las celdas válidas; {Textos.Cantidad(resultado.Errores.Count, "celda quedó", "celdas quedaron")} sin guardar.";
        }

        return Page();
    }

    private async Task<IActionResult?> CargarAsync(Guid id, string? sociedad, string? version, int? anio, CancellationToken ct)
    {
        var vista = await servicio.ObtenerAsync(id, sociedad, version, anio, ct);
        if (vista is null)
        {
            return NotFound();
        }

        var cliente = vista.Contexto.Cliente;
        if (!ReglaAvance.PuedeAbrir(cliente, Pasos.Reportes) || !cliente.Modulos.Contains(Modulo.Presupuesto))
        {
            return Redirect(Navegacion.Destino(cliente));
        }

        Vista = vista;
        ViewData["Cliente"] = cliente;
        return null;
    }
}
