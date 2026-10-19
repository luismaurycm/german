using System.IO.Compression;
using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Core.Reportes;
using Acelerador.Sap;

namespace Acelerador.Core.Servicios;

/// <summary>Una fila del catálogo de UX-10: el reporte y su última generación (o null si nunca se generó).</summary>
public sealed record FilaCatalogo(DefinicionReporte Definicion, ReporteGenerado? Generado);

public sealed record VistaCatalogo(Cliente Cliente, IReadOnlyList<FilaCatalogo> Reportes, IReadOnlyList<Modulo> Modulos, Modulo? Filtro)
{
    /// <summary>Los reportes se habilitan cuando la ejecución terminó (UX-10, estado vacío).</summary>
    public bool Habilitado => Cliente.Estado == EstadoCliente.Completo;
}

/// <summary>El paquete .zip de reportes y lo que pasó con cada uno.</summary>
public sealed record PaqueteReportes(byte[] Zip, string Archivo, IReadOnlyList<ReporteGenerado> Generados);

/// <summary>Lo que muestra UX-11 para un reporte: contexto, parámetros, el reporte calculado y, si se pidió, el detalle de un rubro.</summary>
public sealed record VistaReporteEpm(
    ContextoCierre Contexto,
    DefinicionReporte Definicion,
    ParametrosReporte Parametros,
    IReadOnlyList<Sociedad> Sociedades,
    ReporteCalculado? Reporte,
    ReporteCalculado? Detalle,
    string? Error);

/// <summary>
/// HU-42 a HU-45 y HU-48: el catálogo de reportes de los módulos elegidos (UX-10), su generación como Excel con EPM y el
/// paquete .zip, y la consulta de cada reporte con la estructura común de UX-11 sobre el modelo de consolidación de BPC.
/// </summary>
public sealed class ServicioReportes(
    ContextoCierreLector contexto,
    IReporteRepository reportes,
    AccesoSap acceso,
    ISapGateway sap,
    IProveedorPlantillas plantillas,
    IRegistroEjecuciones registro,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string PasoGenerar = "Generar reportes";
    public const string PasoDescargar = "Descargar reportes";

    public async Task<VistaCatalogo?> CatalogoAsync(Guid clienteId, Modulo? filtro, CancellationToken ct = default)
    {
        var c = await contexto.LeerAsync(clienteId, null, ct);
        if (c is null)
        {
            return null;
        }

        var generados = await reportes.ListarAsync(clienteId, ct);
        var del = CatalogoReportes.DelCliente(c.Cliente);
        var filas = del.Where(r => filtro is null || r.Modulo == filtro)
            .Select(r => new FilaCatalogo(r, generados.FirstOrDefault(g => g.Reporte == r.Id)))
            .ToList();
        return new VistaCatalogo(c.Cliente, filas, del.Select(r => r.Modulo).Distinct().ToList(), filtro);
    }

    /// <summary>«Generar seleccionados» (HU-48): genera cada Excel y deja su estado; un reporte con error guarda la causa (CA4).</summary>
    public async Task<IReadOnlyList<ReporteGenerado>> GenerarAsync(Guid clienteId, IReadOnlyList<string> ids, CancellationToken ct = default)
    {
        var (generados, _) = await GenerarArchivosAsync(clienteId, ids, ct);
        await registro.RegistrarAsync(new Ejecucion(reloj.GetUtcNow(), usuario.Nombre, clienteId, "Acelerador", PasoGenerar, string.Join(", ", generados.Select(g => g.Reporte)),
            generados.All(g => g.Exitoso) ? ResultadoEjecucion.Exitoso : ResultadoEjecucion.Fallido, Resumen(generados)), ct);
        return generados;
    }

    /// <summary>«Descargar reportes (.zip)» (HU-48 CA3): genera los reportes disponibles de los módulos elegidos y los empaca.</summary>
    public async Task<PaqueteReportes?> DescargarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var c = await contexto.LeerAsync(clienteId, null, ct);
        if (c is null)
        {
            return null;
        }

        var ids = CatalogoReportes.DelCliente(c.Cliente).Where(r => r.Disponible).Select(r => r.Id).ToList();
        var (generados, archivos) = await GenerarArchivosAsync(clienteId, ids, ct);
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (nombre, contenido) in archivos)
            {
                var entrada = zip.CreateEntry(nombre, CompressionLevel.Optimal);
                entrada.LastWriteTime = reloj.GetUtcNow();
                await using var s = entrada.Open();
                await s.WriteAsync(contenido, ct);
            }
        }

        var archivo = $"Reportes_{Formato(c.Cliente.Nombre)}_{c.Periodo}.zip";
        await registro.RegistrarAsync(new Ejecucion(reloj.GetUtcNow(), usuario.Nombre, clienteId, "Acelerador", PasoDescargar, $"{archivo} ({archivos.Count} archivos)",
            ResultadoEjecucion.Exitoso, Resumen(generados)), ct);
        return new PaqueteReportes(memoria.ToArray(), archivo, generados);
    }

    /// <summary>UX-11 (HU-42 a HU-45): el reporte con los parámetros elegidos, leído de BPC; con <paramref name="rubro"/>, también su detalle (HU-43 CA3).</summary>
    public async Task<VistaReporteEpm?> ConsultarAsync(Guid clienteId, string id, string? sociedad, bool? filiales, string? moneda, string? periodo, string? rubro, CancellationToken ct = default)
    {
        var d = CatalogoReportes.Buscar(id);
        var c = await contexto.LeerAsync(clienteId, periodo, ct);
        if (d is null || c is null || !d.EsEstadoFinanciero || !d.Disponible)
        {
            return null;
        }

        var matriz = c.Sociedades.Count == 0 ? "" : ReglasCierre.Matriz(c.Sociedades).Codigo;
        var p = new ParametrosReporte(
            sociedad is not null && c.Sociedades.Any(s => s.Codigo == sociedad) ? sociedad : matriz,
            filiales ?? true,
            moneda is not null && c.Monedas.Contains(moneda) ? moneda : c.Monedas.FirstOrDefault() ?? "",
            c.Periodo);
        var anterior = ModeloPropiedad.PeriodoAnterior(c.PeriodoBpc);
        var (actual, error) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(MotorConsolidacion.Modelo, c.PeriodoBpc, p.Moneda), ct);
        if (actual is null)
        {
            return new VistaReporteEpm(c, d, p, c.Sociedades, null, null, error);
        }

        var (previo, _) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(MotorConsolidacion.Modelo, anterior, p.Moneda), ct);
        var (propiedad, _) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(ModeloPropiedad.Modelo), ct);
        var datos = new DatosReporte(actual.Registros, previo?.Registros ?? [], await contexto.AsignacionesAsync(clienteId, c.Sociedades, ct), c.Sociedades,
            ModeloPropiedad.Vigentes(propiedad?.Registros ?? [], c.Grupo, c.PeriodoBpc).Participaciones, c.Grupo);
        var reporte = MotorReportes.Calcular(d, p, datos);
        var detalle = rubro is not null && reporte.Filas.Any(f => f.Rubro == rubro) ? MotorReportes.Detalle(d, p, datos, rubro) : null;
        return new VistaReporteEpm(c, d, p, c.Sociedades, reporte, detalle, null);
    }

    /// <summary>Si los reportes de consolidación del cierre (olas 1 y 2) están generados sin error, para C04.</summary>
    public static bool CierreGenerado(IReadOnlyList<ReporteGenerado> generados) =>
        CatalogoReportes.DelCierre.All(r => generados.Any(g => g.Reporte == r.Id && g.Exitoso));

    private async Task<(IReadOnlyList<ReporteGenerado> Generados, IReadOnlyList<(string Nombre, byte[] Contenido)> Archivos)> GenerarArchivosAsync(
        Guid clienteId, IReadOnlyList<string> ids, CancellationToken ct)
    {
        var c = await contexto.LeerAsync(clienteId, null, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        var asignaciones = await contexto.AsignacionesAsync(clienteId, c.Sociedades, ct);
        var ahora = reloj.GetUtcNow();
        var generados = new List<ReporteGenerado>();
        var archivos = new List<(string, byte[])>();
        foreach (var d in ids.Select(CatalogoReportes.Buscar).OfType<DefinicionReporte>().Where(r => c.Cliente.Modulos.Contains(r.Modulo)).DistinctBy(r => r.Id))
        {
            var (contenido, causa) = Generar(d, c, asignaciones);
            generados.Add(new ReporteGenerado(clienteId, d.Id, causa is null, causa, ahora, usuario.Nombre));
            if (contenido is not null)
            {
                archivos.Add((d.Archivo, contenido));
            }
        }

        await reportes.GuardarAsync(generados, ct);
        return (generados, archivos);
    }

    /// <summary>El Excel de un reporte, o la causa por la que no se generó (HU-48 CA4).</summary>
    private (byte[]? Contenido, string? Causa) Generar(DefinicionReporte d, ContextoCierre c, IReadOnlyList<AsignacionCuenta> asignaciones)
    {
        if (!d.Disponible)
        {
            return (null, "Llega con la ola 3 (HU-46, HU-47) o con su módulo (HU-49 a HU-53).");
        }

        if (c.Sociedades.Count == 0)
        {
            return (null, "El cliente no tiene sociedades registradas.");
        }

        if (d.Id == "R04" && ReglasFlujo.SinClasificar(asignaciones.Select(a => a.Rubro)) is { Count: > 0 } sinClasificar)
        {
            return (null, ReglasFlujo.Error(sinClasificar));
        }

        try
        {
            var parametros = d.Id switch
            {
                "C02" => Definicion.Json(GeneradorPropiedad.ParametrosC02(c.Sociedades, c.Periodo)),
                "C01" or "C03" or "C04" => Definicion.Json(GeneradorCierre.Parametros(c.Sociedades, c.Periodo)),
                _ => Definicion.Json(ParametrosEstado(d, c, asignaciones)),
            };
            return (plantillas.Generar(d.EsEstadoFinanciero ? "R" : d.Id, parametros), null);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.Text.Json.JsonException)
        {
            return (null, $"No se pudo generar el Excel: {ex.Message}");
        }
    }

    /// <summary>Parámetros del Excel de un reporte de la estructura común: los de UX-11 y las filas del reporte (rubros o cuentas).</summary>
    private static object ParametrosEstado(DefinicionReporte d, ContextoCierre c, IReadOnlyList<AsignacionCuenta> asignaciones)
    {
        var matriz = ReglasCierre.Matriz(c.Sociedades).Codigo;
        var p = new ParametrosReporte(matriz, true, c.Monedas[0], c.Periodo);
        var estructura = MotorReportes.Calcular(d, p, new DatosReporte([], [], asignaciones, c.Sociedades, [], c.Grupo));
        return new
        {
            id = d.Id,
            nombre = d.Nombre,
            conexion = GeneradorCierre.Conexion,
            grupo = c.Grupo,
            periodo = MotorConsolidacion.PeriodoBpc(c.Periodo),
            monedas = c.Monedas,
            sociedades = c.Sociedades.Select(s => new { codigo = s.Codigo, nombre = s.Nombre }),
            encabezados = estructura.Encabezados,
            filas = estructura.Filas.Select(f => new { tipo = f.Tipo.ToString(), etiquetas = f.Etiquetas, rubro = f.Rubro }),
        };
    }

    private static string Resumen(IReadOnlyList<ReporteGenerado> generados)
    {
        var errores = generados.Where(g => !g.Exitoso).ToList();
        return errores.Count == 0
            ? $"{Textos.Cantidad(generados.Count, "reporte generado", "reportes generados")}."
            : $"{generados.Count - errores.Count} generados; con error: {string.Join(" · ", errores.Select(e => $"{e.Reporte}: {e.Mensaje}"))}";
    }

    private static string Formato(string nombre) => string.Join("_", nombre.Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
