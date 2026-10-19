using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;

namespace Acelerador.Core.Servicios;

public sealed record VistaRegistro(Cliente Cliente, IReadOnlyList<Ejecucion> Ejecuciones, int Total, ResultadoEjecucion? Filtro);

public sealed record ArchivoRegistro(string Nombre, byte[] Contenido);

/// <summary>Casos de uso de HU-54: consultar, filtrar y descargar el registro de ejecuciones (vista de registro de UX-09).</summary>
public sealed class ServicioRegistro(IClienteRepository clientes, IRegistroEjecuciones registro, IDepuracionRegistro depuracion, TimeProvider reloj)
{
    public async Task<VistaRegistro?> ObtenerAsync(Guid clienteId, ResultadoEjecucion? filtro, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is null)
        {
            return null;
        }

        var todas = await registro.ListarAsync(clienteId, ct);
        return new VistaRegistro(cliente, todas.Where(e => filtro is null || e.Resultado == filtro).ToList(), todas.Count, filtro);
    }

    /// <summary>Archivo con los mismos campos de la pantalla (HU-54 CA3).</summary>
    public async Task<ArchivoRegistro?> ExportarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is null)
        {
            return null;
        }

        var nombre = $"registro-ejecuciones-{Archivo(cliente.Nombre)}-{reloj.GetUtcNow().ToLocalTime():yyyyMMdd-HHmm}.csv";
        return new ArchivoRegistro(nombre, ReglasRegistro.Csv(await registro.ListarAsync(clienteId, ct)));
    }

    /// <summary>Quita las entradas de más de 12 meses (RNF-Aud-01). Se ejecuta al arrancar la aplicación.</summary>
    public Task<int> DepurarAsync(CancellationToken ct = default) =>
        depuracion.DepurarAnterioresAAsync(ReglasRegistro.Corte(reloj.GetUtcNow()), ct);

    private static string Archivo(string nombre)
    {
        var limpio = new string(nombre.Normalize(System.Text.NormalizationForm.FormD)
            .Where(c => char.IsAsciiLetterOrDigit(c) || c == ' ')
            .ToArray());
        return string.Join('-', limpio.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
