using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Acelerador.Core.Servicios;

/// <summary>Todo lo que muestra UX-02 para un cliente.</summary>
public sealed record VistaConexiones(
    Cliente Cliente,
    IReadOnlyList<Conexion> Conexiones,
    bool AplicaValidacionErpBw,
    bool PuedeValidarErpBw,
    Clasificacion? Clasificacion,
    IReadOnlyList<string> Pendientes)
{
    public bool PuedeContinuar => Pendientes.Count == 0;

    public Conexion this[SistemaSap sistema] => Conexiones.First(c => c.Sistema == sistema);
}

public sealed record ResultadoProbar(IReadOnlyDictionary<string, string> Errores, Conexion? Conexion, string? ErrorDeteccion = null)
{
    public bool Valido => Errores.Count == 0;
}

public sealed record ResultadoOperacion(bool Exitosa, string Mensaje);

/// <summary>
/// Orquesta el paso de Conexiones: HU-03 (registrar y probar), HU-04 (credenciales en el almacén),
/// HU-05 (validación ERP–BW), HU-06 y HU-57 (arquitectura y versión). Cada llamada a SAP queda en el
/// registro de ejecuciones (RNF-Aud-01) con el mensaje sanitizado (RNF-Seg-02).
/// </summary>
public sealed class ServicioConexiones(
    IClienteRepository clientes,
    IConexionRepository conexiones,
    ICredentialStore credenciales,
    IRegistroEjecuciones registro,
    ISapGateway sap,
    CatalogoVariantes catalogo,
    IUsuarioActual usuario,
    TimeProvider reloj,
    ILogger<ServicioConexiones>? logger = null)
{
    public const string PasoProbar = "Probar conexión";
    public const string PasoValidarErpBw = "Validar conexión ERP–BW";
    public const string PasoDetectar = "Identificar arquitectura y versión";
    public const string SinObjetos = "Ninguno (solo lectura)";
    public const string MensajeCredencialNoDisponible =
        "La credencial guardada no está disponible en este equipo. Escríbela de nuevo.";

    private readonly ILogger _log = logger ?? NullLogger<ServicioConexiones>.Instance;

    /// <summary>Referencia con la que se guarda la credencial en el almacén. Es lo único que va a SQLite.</summary>
    public static string ReferenciaCredencial(Guid clienteId, SistemaSap sistema) =>
        $"CMConsulting.Acelerador/{clienteId:N}/{sistema}";

    public async Task<VistaConexiones?> ObtenerAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        return cliente is null ? null : await ArmarVistaAsync(cliente, ct);
    }

    public async Task<ResultadoProbar> GuardarYProbarAsync(Guid clienteId, SistemaSap sistema, DatosConexion datos, CancellationToken ct = default)
    {
        var cliente = await ClienteAsync(clienteId, ct);
        var lista = await ListaCompletaAsync(clienteId, ct);
        var conexion = lista.First(c => c.Sistema == sistema);

        var errores = ReglasConexion.Validar(datos, conexion.TieneCredencial);
        if (errores.Count > 0)
        {
            return new ResultadoProbar(errores, null);
        }

        if (ReglasConexion.Cambio(conexion, datos))
        {
            conexion.ReiniciarPrueba();
            InvalidarDiagnostico(cliente, sistema);
        }

        conexion.Host = datos.Host!.Trim();
        conexion.Mandante = datos.Mandante!.Trim();
        conexion.UsuarioTecnico = datos.UsuarioTecnico!.Trim().ToUpperInvariant();
        if (!string.IsNullOrEmpty(datos.CredencialNueva))
        {
            var referencia = ReferenciaCredencial(clienteId, sistema);
            credenciales.Guardar(referencia, conexion.UsuarioTecnico, new CredencialSap(datos.CredencialNueva));
            conexion.ReferenciaCredencial = referencia;
        }

        await conexiones.GuardarAsync(conexion, ct);
        await clientes.ActualizarAsync(cliente, ct);

        await ProbarAsync(cliente, conexion, ct);

        string? errorDeteccion = null;
        if (cliente.Deteccion is null && lista.All(c => c.Estado == EstadoPrueba.Exitosa))
        {
            var deteccion = await DetectarAsync(clienteId, ct);
            errorDeteccion = deteccion.Exitosa ? null : deteccion.Mensaje;
        }

        return new ResultadoProbar(errores, conexion, errorDeteccion);
    }

    public async Task<ResultadoOperacion> ValidarErpBwAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await ClienteAsync(clienteId, ct);
        var lista = await ListaCompletaAsync(clienteId, ct);
        var erp = lista.First(c => c.Sistema == SistemaSap.Erp);
        var bw = lista.First(c => c.Sistema == SistemaSap.Bw);

        if (!ReglaRn01.AplicaValidacionErpBw(erp, bw))
        {
            return new ResultadoOperacion(false, "ERP y BW están en el mismo sistema; no requiere validación (RN-01).");
        }

        if (!ReglaRn01.PuedeValidar(erp, bw))
        {
            return new ResultadoOperacion(false, "Prueba primero con éxito las conexiones a SAP ERP y SAP BW.");
        }

        var credErp = credenciales.Leer(erp.ReferenciaCredencial!);
        var credBw = credenciales.Leer(bw.ReferenciaCredencial!);
        ResultadoValidacionErpBw resultado;
        if (credErp is null || credBw is null)
        {
            resultado = new ResultadoValidacionErpBw(false, MensajeCredencialNoDisponible);
        }
        else
        {
            try
            {
                resultado = await sap.ValidarConexionErpBwAsync(Destino(erp, credErp), Destino(bw, credBw), ct);
                resultado = resultado with { Mensaje = SanitizadorMensajes.Limpiar(resultado.Mensaje, credErp, credBw) };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resultado = new ResultadoValidacionErpBw(false, ErrorInesperado(ex, credErp, credBw));
            }
        }

        var ahora = reloj.GetUtcNow();
        cliente.Validacion = new ValidacionErpBw(resultado.Exitosa, ahora, usuario.Nombre, resultado.Mensaje);
        await clientes.ActualizarAsync(cliente, ct);
        await RegistrarAsync(cliente.Id, "SAP ERP → SAP BW", PasoValidarErpBw,
            resultado.Exitosa ? ResultadoEjecucion.Exitoso : ResultadoEjecucion.Fallido, resultado.Mensaje, ct);
        return new ResultadoOperacion(resultado.Exitosa, resultado.Mensaje);
    }

    /// <summary>Lee el paisaje, clasifica la arquitectura y la guarda en el cliente (HU-06, HU-57).</summary>
    public async Task<ResultadoOperacion> DetectarAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await ClienteAsync(clienteId, ct);
        var lista = await ListaCompletaAsync(clienteId, ct);
        if (lista.Any(c => c.Estado != EstadoPrueba.Exitosa))
        {
            return new ResultadoOperacion(false, "Prueba primero con éxito las tres conexiones.");
        }

        var creds = lista.Select(c => credenciales.Leer(c.ReferenciaCredencial!)).ToArray();
        if (creds.Any(c => c is null))
        {
            await RegistrarAsync(cliente.Id, "SAP ERP, SAP BW, SAP BPC", PasoDetectar, ResultadoEjecucion.Fallido, MensajeCredencialNoDisponible, ct);
            return new ResultadoOperacion(false, MensajeCredencialNoDisponible);
        }

        ResultadoDeteccion resultado;
        try
        {
            resultado = await sap.DetectarPaisajeAsync(
                Destino(lista[0], creds[0]!), Destino(lista[1], creds[1]!), Destino(lista[2], creds[2]!), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            resultado = new ResultadoDeteccion(false, ErrorInesperado(ex, creds));
        }

        if (!resultado.Exitosa || resultado.Paisaje is null)
        {
            var mensaje = SanitizadorMensajes.Limpiar(resultado.Mensaje, creds);
            await RegistrarAsync(cliente.Id, "SAP ERP, SAP BW, SAP BPC", PasoDetectar, ResultadoEjecucion.Fallido, mensaje, ct);
            return new ResultadoOperacion(false, mensaje);
        }

        var p = resultado.Paisaje;
        var clasificacion = ClasificadorArquitectura.Clasificar(p, catalogo);
        cliente.Deteccion = new DeteccionArquitectura(clasificacion.Arquitectura, p.Bw, p.Bpc, clasificacion.Variante?.Id, reloj.GetUtcNow());
        if (clasificacion.Soportada)
        {
            cliente.Estado = EstadoCliente.EnCurso;
            cliente.MotivoEstado = null;
        }
        else
        {
            cliente.Estado = EstadoCliente.NoSoportado;
            cliente.MotivoEstado = clasificacion.Arquitectura == Arquitectura.B
                ? $"{clasificacion.Titulo} (RN-19)."
                : $"{clasificacion.Titulo}: {Textos.Version(p.Bw)} con {Textos.Version(p.Bpc)}.";
        }

        await clientes.ActualizarAsync(cliente, ct);
        var detalle = $"{clasificacion.Titulo}. {Textos.Arquitectura(cliente.Deteccion)}. SID ERP {p.SidErp}, BW {p.SidBw}, BPC {p.SidBpc}.";
        await RegistrarAsync(cliente.Id, "SAP ERP, SAP BW, SAP BPC", PasoDetectar,
            clasificacion.Soportada ? ResultadoEjecucion.Exitoso : ResultadoEjecucion.NoSoportado, detalle, ct);
        return new ResultadoOperacion(true, clasificacion.Titulo);
    }

    /// <summary>«Continuar al diagnóstico»: valida en el servidor lo mismo que habilita el botón.</summary>
    public async Task<IReadOnlyList<string>> ContinuarADiagnosticoAsync(Guid clienteId, CancellationToken ct = default)
    {
        var cliente = await ClienteAsync(clienteId, ct);
        var vista = await ArmarVistaAsync(cliente, ct);
        if (vista.PuedeContinuar)
        {
            cliente.AvanzarA(Pasos.Diagnostico);
            await clientes.ActualizarAsync(cliente, ct);
        }

        return vista.Pendientes;
    }

    private async Task ProbarAsync(Cliente cliente, Conexion conexion, CancellationToken ct)
    {
        var credencial = credenciales.Leer(conexion.ReferenciaCredencial!);
        ResultadoPrueba resultado;
        if (credencial is null)
        {
            resultado = new ResultadoPrueba(false, MensajeCredencialNoDisponible);
        }
        else
        {
            try
            {
                resultado = await sap.ProbarConexionAsync(Destino(conexion, credencial), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                resultado = new ResultadoPrueba(false, ErrorInesperado(ex, credencial));
            }
        }

        var mensaje = SanitizadorMensajes.Limpiar(resultado.Mensaje, credencial);
        conexion.Estado = resultado.Exitosa ? EstadoPrueba.Exitosa : EstadoPrueba.Fallida;
        conexion.Mensaje = mensaje;
        conexion.FechaPrueba = reloj.GetUtcNow();
        conexion.Sid = resultado.Exitosa ? resultado.Sistema?.Sid : null;
        conexion.Producto = resultado.Exitosa ? resultado.Sistema?.Producto : null;
        await conexiones.GuardarAsync(conexion, ct);

        await RegistrarAsync(cliente.Id, Textos.Sistema(conexion.Sistema), PasoProbar,
            resultado.Exitosa ? ResultadoEjecucion.Exitoso : ResultadoEjecucion.Fallido,
            $"{conexion.Host}, mandante {conexion.Mandante}, usuario {conexion.UsuarioTecnico}: {mensaje}", ct);
    }

    /// <summary>Si cambian los datos de un sistema, la validación ERP–BW y la detección anteriores ya no valen.</summary>
    private static void InvalidarDiagnostico(Cliente cliente, SistemaSap sistema)
    {
        if (sistema is SistemaSap.Erp or SistemaSap.Bw)
        {
            cliente.Validacion = null;
        }

        cliente.Deteccion = null;
        cliente.Estado = EstadoCliente.EnCurso;
        cliente.MotivoEstado = null;
    }

    private async Task<VistaConexiones> ArmarVistaAsync(Cliente cliente, CancellationToken ct)
    {
        var lista = await ListaCompletaAsync(cliente.Id, ct);
        var erp = lista.First(c => c.Sistema == SistemaSap.Erp);
        var bw = lista.First(c => c.Sistema == SistemaSap.Bw);
        var clasificacion = cliente.Deteccion is null ? null : ClasificadorArquitectura.Describir(cliente.Deteccion, catalogo);
        return new VistaConexiones(
            cliente,
            lista,
            ReglaRn01.AplicaValidacionErpBw(erp, bw),
            ReglaRn01.PuedeValidar(erp, bw),
            clasificacion,
            ReglaAvance.PendientesParaDiagnostico(cliente, lista, catalogo));
    }

    /// <summary>Las tres conexiones en orden ERP, BW, BPC; las que no existen se crean vacías.</summary>
    private async Task<IReadOnlyList<Conexion>> ListaCompletaAsync(Guid clienteId, CancellationToken ct)
    {
        var guardadas = await conexiones.ListarAsync(clienteId, ct);
        return Enum.GetValues<SistemaSap>()
            .Select(s => guardadas.FirstOrDefault(c => c.Sistema == s) ?? Conexion.Nueva(clienteId, s))
            .ToList();
    }

    private async Task<Cliente> ClienteAsync(Guid id, CancellationToken ct) =>
        await clientes.ObtenerAsync(id, ct) ?? throw new KeyNotFoundException($"No existe el cliente {id}.");

    private static DestinoSap Destino(Conexion c, CredencialSap credencial) => new(c.Sistema, c.Parametros, credencial);

    private string ErrorInesperado(Exception ex, params CredencialSap?[] creds)
    {
        _log.LogError("Error inesperado al llamar a SAP: {Tipo}", ex.GetType().Name);
        return SanitizadorMensajes.Limpiar($"Error inesperado al llamar a SAP: {ex.Message}", creds);
    }

    private Task RegistrarAsync(Guid clienteId, string sistema, string paso, ResultadoEjecucion resultado, string mensaje, CancellationToken ct) =>
        registro.RegistrarAsync(new Ejecucion(reloj.GetUtcNow(), usuario.Nombre, clienteId, sistema, paso, SinObjetos, resultado, mensaje), ct);
}
