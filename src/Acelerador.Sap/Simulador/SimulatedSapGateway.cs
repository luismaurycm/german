using System.Collections.Concurrent;

namespace Acelerador.Sap.Simulador;

/// <summary>Una llamada recibida por el simulador. Sirve para comprobar que nada escribe en el cliente (RN-19).</summary>
public sealed record LlamadaSimulada(string Operacion, SistemaSap Sistema, bool EsEscritura);

/// <summary>
/// Implementación de <see cref="ISapGateway"/> sin SAP: imita los paisajes de <see cref="PerfilSimulado"/>,
/// los fallos de conexión y los mensajes que devuelve SAP. Se usa en las pruebas unitarias y en
/// desarrollo mientras la prueba técnica HU-01 no confirme el conector real.
/// </summary>
public sealed class SimulatedSapGateway : ISapGateway
{
    public static string MensajeNoAlcanzable(string host) => $"Partner '{host}:sapgw00' not reached";
    public const string MensajeClaveIncorrecta = "Name or password is incorrect (repeat logon)";
    public static string MensajeFuenteInactiva(string sidErp, string mandante) =>
        $"El sistema fuente {sidErp}CLNT{mandante} no está activo en BW (transacción RSA1).";
    public const string MensajeFalloDeteccion = "No se pudo leer la tabla de componentes CVERS: falta la autorización S_TABU_NAM.";

    private readonly Func<OpcionesSimulador> _opciones;
    private readonly ConcurrentQueue<LlamadaSimulada> _llamadas = new();

    public SimulatedSapGateway(OpcionesSimulador opciones)
        : this(() => opciones)
    {
    }

    /// <summary>Permite leer la configuración en cada llamada (cambios en caliente de appsettings).</summary>
    public SimulatedSapGateway(Func<OpcionesSimulador> opciones)
    {
        _opciones = opciones;
    }

    public IReadOnlyCollection<LlamadaSimulada> Llamadas => _llamadas.ToArray();

    public async Task<ResultadoPrueba> ProbarConexionAsync(DestinoSap destino, CancellationToken ct = default)
    {
        var o = _opciones();
        Registrar("RFC_PING + RFC_SYSTEM_INFO", destino.Sistema);
        await Esperar(o, ct);

        var p = destino.Parametros;
        if (o.SistemasConFallo.Contains(destino.Sistema) || Contiene(p.Host, "falla"))
        {
            return new ResultadoPrueba(false, MensajeNoAlcanzable(p.Host));
        }

        if (string.Equals(destino.Credencial.Revelar(), "invalida", StringComparison.OrdinalIgnoreCase) || o.EcoDeCredencialEnErrores)
        {
            var mensaje = o.EcoDeCredencialEnErrores
                ? $"{MensajeClaveIncorrecta}: user {p.UsuarioTecnico} password {destino.Credencial.Revelar()}"
                : MensajeClaveIncorrecta;
            return new ResultadoPrueba(false, mensaje);
        }

        var paisaje = Paisaje(o.Perfil);
        var info = destino.Sistema switch
        {
            SistemaSap.Erp => new InfoSistemaSap(paisaje.SidErp, ProductoErp(o.Perfil)),
            SistemaSap.Bw => new InfoSistemaSap(paisaje.SidBw, ProductoBwTexto(o.Perfil)),
            _ => new InfoSistemaSap(paisaje.SidBpc, ProductoBpcTexto(o.Perfil)),
        };
        return new ResultadoPrueba(true, $"Conectado a {info.Sid}, mandante {p.Mandante}.", info);
    }

    public async Task<ResultadoValidacionErpBw> ValidarConexionErpBwAsync(DestinoSap erp, DestinoSap bw, CancellationToken ct = default)
    {
        var o = _opciones();
        Registrar("Z_ACC_BW_VALIDAR_FUENTE", SistemaSap.Bw);
        await Esperar(o, ct);

        var sidErp = Paisaje(o.Perfil).SidErp;
        if (o.FallaValidacionErpBw || Contiene(bw.Parametros.Host, "sinfuente"))
        {
            return new ResultadoValidacionErpBw(false, MensajeFuenteInactiva(sidErp, erp.Parametros.Mandante));
        }

        return new ResultadoValidacionErpBw(true, $"BW lee del sistema fuente {sidErp}CLNT{erp.Parametros.Mandante}: conexión activa y extractores accesibles.");
    }

    public async Task<ResultadoDeteccion> DetectarPaisajeAsync(DestinoSap erp, DestinoSap bw, DestinoSap bpc, CancellationToken ct = default)
    {
        var o = _opciones();
        Registrar("Z_ACC_SISTEMA_INFO", SistemaSap.Erp);
        Registrar("Z_ACC_SISTEMA_INFO", SistemaSap.Bw);
        Registrar("Z_ACC_BPC_INFO", SistemaSap.Bpc);
        await Esperar(o, ct);

        if (o.FallaDeteccion)
        {
            return new ResultadoDeteccion(false, MensajeFalloDeteccion);
        }

        return new ResultadoDeteccion(true, "Componentes leídos de los tres sistemas.", Paisaje(o.Perfil));
    }

    public static PaisajeSap Paisaje(PerfilSimulado perfil) => perfil switch
    {
        PerfilSimulado.ABw75 => new("ECP", "BWP", "BWP", new(ProductoBw.Bw75, "7.5"), new(ProductoBpc.Bpc101, "10.1", ModeloBpc.Estandar)),
        PerfilSimulado.ABw4Hana => new("S4P", "B4P", "B4P", new(ProductoBw.Bw4Hana, "2.0"), new(ProductoBpc.Bpc2021, "2021", ModeloBpc.Estandar)),
        PerfilSimulado.AVersionNoSoportada => new("ECP", "BWP", "BWP", new(ProductoBw.NoReconocido, "7.4"), new(ProductoBpc.NoReconocido, "10.0", ModeloBpc.Estandar)),
        PerfilSimulado.ABpcEmbebido => new("ECP", "BWP", "BWP", new(ProductoBw.Bw75, "7.5"), new(ProductoBpc.Bpc101, "10.1", ModeloBpc.Embebido)),
        _ => new("S4H", "S4H", "S4H", new(ProductoBw.NoReconocido, "BW embebido en S/4HANA 2025"), new(ProductoBpc.NoReconocido, "2025", ModeloBpc.Embebido)),
    };

    private static string ProductoErp(PerfilSimulado perfil) => perfil switch
    {
        PerfilSimulado.ABw4Hana => "SAP S/4HANA 2023",
        PerfilSimulado.B => "SAP S/4HANA 2025",
        _ => "SAP ECC 6.0 EHP8",
    };

    private static string ProductoBwTexto(PerfilSimulado perfil) => perfil switch
    {
        PerfilSimulado.ABw4Hana => "SAP BW/4HANA 2.0",
        PerfilSimulado.AVersionNoSoportada => "SAP BW 7.4",
        PerfilSimulado.B => "BW embebido en S/4HANA",
        _ => "SAP BW 7.5",
    };

    private static string ProductoBpcTexto(PerfilSimulado perfil) => perfil switch
    {
        PerfilSimulado.ABw4Hana => "SAP BPC 2021 modelo estándar",
        PerfilSimulado.AVersionNoSoportada => "SAP BPC 10.0 modelo estándar",
        PerfilSimulado.ABpcEmbebido => "SAP BPC 10.1 modelo embebido",
        PerfilSimulado.B => "BPC modelo embebido",
        _ => "SAP BPC 10.1 modelo estándar",
    };

    private void Registrar(string operacion, SistemaSap sistema) =>
        _llamadas.Enqueue(new LlamadaSimulada(operacion, sistema, EsEscritura: false));

    private static Task Esperar(OpcionesSimulador o, CancellationToken ct) =>
        o.LatenciaMs > 0 ? Task.Delay(o.LatenciaMs, ct) : Task.CompletedTask;

    private static bool Contiene(string texto, string valor) =>
        texto.Contains(valor, StringComparison.OrdinalIgnoreCase);
}
