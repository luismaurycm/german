using System.Collections.Concurrent;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Servicios;

/// <summary>
/// Archivos validados que esperan «Confirmar carga». Viven solo en memoria y por un tiempo limitado: los importes de las
/// sociedades no SAP no se guardan en la base local (arquitectura, sección 7).
/// </summary>
public sealed class CargasPendientes(TimeProvider reloj)
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<(Guid, string), (ArchivoNoSap Archivo, DateTimeOffset Vence)> _pendientes = new();

    public void Guardar(Guid clienteId, ArchivoNoSap archivo) =>
        _pendientes[(clienteId, archivo.Sociedad)] = (archivo, reloj.GetUtcNow() + Vigencia);

    public ArchivoNoSap? Obtener(Guid clienteId, string sociedad) =>
        _pendientes.TryGetValue((clienteId, sociedad), out var p) && p.Vence > reloj.GetUtcNow() ? p.Archivo : null;

    public void Quitar(Guid clienteId, string sociedad) => _pendientes.TryRemove((clienteId, sociedad), out _);
}

/// <summary>Una sociedad no SAP en UX-07 con sus cargas confirmadas y el archivo pendiente, si lo hay.</summary>
public sealed record SociedadNoSap(Sociedad Sociedad, IReadOnlyList<CargaNoSap> Cargas, ArchivoNoSap? Pendiente)
{
    public bool Cargada => Cargas.Count > 0;
}

public sealed record VistaNoSap(Cliente Cliente, IReadOnlyList<SociedadNoSap> Sociedades)
{
    public bool TodasCargadas => Sociedades.Count > 0 && Sociedades.All(s => s.Cargada);
}

/// <summary>
/// Casos de uso de HU-25 (UX-07): validar el archivo de cada sociedad no SAP, mostrar la vista previa y los errores, y
/// al confirmar cargarlo a BW por el paquete Z (INT-04 por INT-02). La carga reemplaza la del mismo periodo.
/// </summary>
public sealed class ServicioNoSap(
    IClienteRepository clientes,
    ISociedadRepository sociedades,
    IMapeoRepository mapeo,
    ICargaNoSapRepository cargas,
    CargasPendientes pendientes,
    AccesoSap acceso,
    ISapGateway sap,
    IRegistroEjecuciones registro,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string TablaStaging = "ZACC_NOSAP";
    public const string PasoCarga = "Carga de sociedad no SAP";

    public async Task<VistaNoSap?> ObtenerAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is null)
        {
            return null;
        }

        var lista = await sociedades.ListarAsync(clienteId, ct);
        var hechas = await cargas.ListarAsync(clienteId, ct);
        var noSap = lista.Where(s => !s.EnSap)
            .Select(s => new SociedadNoSap(s, hechas.Where(c => c.Sociedad == s.Codigo).OrderBy(c => c.Periodo).ToList(), pendientes.Obtener(clienteId, s.Codigo)))
            .ToList();
        return new VistaNoSap(cliente, noSap);
    }

    /// <summary>Lee y valida el archivo (HU-25 CA1, CA2 y CA4) y lo deja pendiente de confirmar.</summary>
    public async Task<ArchivoNoSap> ValidarAsync(Guid clienteId, string sociedad, string nombre, byte[] contenido, CancellationToken ct = default)
    {
        var lista = await sociedades.ListarAsync(clienteId, ct);
        var noSap = lista.Where(s => !s.EnSap).Select(s => s.Codigo).ToList();
        var soc = lista.FirstOrDefault(s => s.Codigo == sociedad);
        var mapeadas = soc is null
            ? new HashSet<string>()
            : (await mapeo.ListarAsync(clienteId, PlanDeCuentas(soc, lista), ct)).Select(a => a.Cuenta).ToHashSet(StringComparer.Ordinal);

        var archivo = LectorArchivoNoSap.Leer(contenido, nombre, sociedad, noSap, mapeadas);
        pendientes.Guardar(clienteId, archivo);
        return archivo;
    }

    /// <summary>«Confirmar carga» (HU-25 CA3): solo con un archivo válido. Carga todo o nada y lo registra.</summary>
    public async Task<ResultadoOperacion> ConfirmarAsync(Guid clienteId, string sociedad, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        var bloqueo = ReglaAvance.BloqueoDeConfiguracion(cliente);
        if (bloqueo is not null)
        {
            return new ResultadoOperacion(false, bloqueo);
        }

        var archivo = pendientes.Obtener(clienteId, sociedad);
        if (archivo is null)
        {
            return new ResultadoOperacion(false, "Sube de nuevo el archivo: la validación anterior ya no está vigente.");
        }

        if (!archivo.Valido)
        {
            return new ResultadoOperacion(false, "Corrige los errores del archivo antes de confirmar la carga.");
        }

        var destino = await acceso.DestinoAsync(clienteId, SistemaSap.Bw, ct);
        var objeto = $"{TablaStaging} {sociedad} {string.Join(", ", archivo.Periodos)}";
        ResultadoOperacionSap resultado;
        if (destino.Destino is null)
        {
            resultado = new ResultadoOperacionSap(false, destino.Error!);
        }
        else
        {
            var definicion = Definicion.Json(new
            {
                tabla = TablaStaging,
                sociedad,
                periodos = archivo.Periodos,
                reemplazar = true,
                filas = archivo.Filas.Select(f => new { f.Cuenta, f.Periodo, f.Tercero, f.CentroCosto, f.CentroBeneficio, f.Importe }),
            });
            try
            {
                resultado = await sap.EjecutarAsync(destino.Destino, new OperacionSap(FuncionesZ.CargarArchivo, "DATOS", $"{TablaStaging}/{sociedad}", definicion), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resultado = new ResultadoOperacionSap(false, $"Error inesperado al llamar a SAP: {ex.Message}");
            }
        }

        var mensaje = SanitizadorMensajes.Limpiar(resultado.Mensaje, destino.Credencial);
        if (!resultado.Exitosa)
        {
            await RegistrarAsync(clienteId, objeto, ResultadoEjecucion.Fallido, mensaje, ct);
            return new ResultadoOperacion(false, $"No se cargó el archivo: {mensaje}");
        }

        var ahora = reloj.GetUtcNow();
        var porPeriodo = archivo.Filas.GroupBy(f => f.Periodo)
            .Select(g => new CargaNoSap(clienteId, sociedad, g.Key, archivo.Nombre, g.Count(), g.Sum(f => f.Importe), ahora, usuario.Nombre))
            .ToList();
        await cargas.GuardarAsync(porPeriodo, ct);
        pendientes.Quitar(clienteId, sociedad);
        await RegistrarAsync(clienteId, $"{objeto} ({archivo.Filas.Count} filas)", ResultadoEjecucion.Exitoso,
            $"{archivo.Nombre}: {archivo.Filas.Count} filas cargadas en la tabla {TablaStaging} de BW; reemplazan la carga anterior del periodo.", ct);
        return new ResultadoOperacion(true, $"Carga de {sociedad} confirmada: {Textos.Cantidad(archivo.Filas.Count, "fila", "filas")}.");
    }

    public void Descartar(Guid clienteId, string sociedad) => pendientes.Quitar(clienteId, sociedad);

    /// <summary>«Continuar al plan»: exige que todas las sociedades no SAP tengan su carga.</summary>
    public async Task<string?> ContinuarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var vista = await ObtenerAsync(clienteId, ct);
        if (vista is null || (vista.Sociedades.Count > 0 && !vista.TodasCargadas) || ReglaAvance.BloqueoDeConfiguracion(vista.Cliente) is not null)
        {
            return null;
        }

        vista.Cliente.AvanzarA(Pasos.Plan);
        await clientes.ActualizarAsync(vista.Cliente, ct);
        return Pasos.Plan;
    }

    /// <summary>Plan de cuentas de una sociedad no SAP: el suyo, o el de su matriz si no tiene (sus cuentas se mapean en ese plan).</summary>
    public static string PlanDeCuentas(Sociedad sociedad, IReadOnlyList<Sociedad> todas) =>
        sociedad.PlanCuentas.Length > 0
            ? sociedad.PlanCuentas
            : todas.FirstOrDefault(s => s.Codigo == sociedad.Matriz)?.PlanCuentas
              ?? todas.FirstOrDefault(s => s.Metodo == MetodoConsolidacion.Matriz)?.PlanCuentas
              ?? "";

    private Task RegistrarAsync(Guid clienteId, string objetos, ResultadoEjecucion resultado, string mensaje, CancellationToken ct) =>
        registro.RegistrarAsync(new Ejecucion(reloj.GetUtcNow(), usuario.Nombre, clienteId, Textos.Sistema(SistemaSap.Bw), PasoCarga, objetos, resultado, mensaje), ct);
}
