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
    public const string MensajeFalloSociedades = "BAPI_COMPANYCODE_GETLIST: no tiene autorización para ver sociedades (F_BKPF_BUK).";
    public const string MensajeFalloCuentas = "BAPI_GL_ACC_GETLIST: no tiene autorización para el plan de cuentas (F_SKA1_KTP).";
    public const string PeriodoSaldos = "2026-09";

    public static string MensajeFalloInventario(TipoInventario tipo) => tipo switch
    {
        TipoInventario.Extractores => "Z_ACC_INVENTARIO: sin autorización para leer ROOSOURCE (S_TABU_NAM).",
        TipoInventario.InfoProviders => "Z_ACC_INVENTARIO: sin autorización de visualización en BW (S_RS_ADMWB, actividad 03).",
        TipoInventario.ModelosBpc => "Z_ACC_INVENTARIO: el usuario técnico no tiene permiso de lectura sobre los modelos BPC (S_RS_AUTH).",
        _ => "Z_ACC_INVENTARIO: sin autorización para leer DD03L (S_TABU_NAM).",
    };

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

    public async Task<ResultadoInventario> LeerInventarioAsync(DestinoSap destino, TipoInventario tipo, CancellationToken ct = default)
    {
        var o = _opciones();
        Registrar($"Z_ACC_INVENTARIO {tipo}", destino.Sistema);
        await Esperar(o, ct);

        if (o.FallaInventario == tipo || (tipo == TipoInventario.ModelosBpc && Contiene(destino.Parametros.Host, "sinpermiso")))
        {
            return new ResultadoInventario(false, MensajeFalloInventario(tipo), []);
        }

        if (o.InventarioVacio || (tipo is TipoInventario.Extractores or TipoInventario.CamposZ && Contiene(destino.Parametros.Host, "vacio")))
        {
            return new ResultadoInventario(true, "Sin objetos.", []);
        }

        var s4 = o.Perfil is PerfilSimulado.ABw4Hana or PerfilSimulado.B;
        var bw4 = o.Perfil == PerfilSimulado.ABw4Hana;
        var bodega = o.BodegaExistente || (tipo == TipoInventario.InfoProviders && Contiene(destino.Parametros.Host, "bodega"));
        IReadOnlyList<ObjetoInventario> objetos = tipo switch
        {
            TipoInventario.Extractores =>
            [
                new(s4 ? "0FI_ACDOCA_10" : "0FI_GL_14", "Partidas individuales de contabilidad general", true),
                new("0COSTCENTER_ATTR", "Centros de costo (atributos)", true),
                new("0PROFIT_CTR_ATTR", "Centros de beneficio (atributos)", true),
                new("0FI_AA_11", "Movimientos de activos fijos", false),
            ],
            TipoInventario.InfoProviders => InfoProviders(bw4, bodega),
            TipoInventario.ModelosBpc =>
            [
                new("PLAN_VENTAS", "Planeación de ventas del área comercial (ejemplo)", true, "Ambiente ENV_COMERCIAL"),
            ],
            _ =>
            [
                new("ZZREGION", "Región comercial (ejemplo)", true, s4 ? "ACDOCA" : "BSEG"),
                new("ZZLINEA", "Línea de negocio (ejemplo)", true, s4 ? "ACDOCA" : "BSEG"),
                .. o.CamposZAdicionales.Select(c => new ObjetoInventario(c, "Campo agregado por el cliente", true, s4 ? "ACDOCA" : "BSEG")),
            ],
        };
        return new ResultadoInventario(true, $"{objetos.Count} objetos leídos.", objetos);
    }

    public async Task<ResultadoSociedades> LeerSociedadesAsync(DestinoSap erp, CancellationToken ct = default)
    {
        var o = _opciones();
        Registrar("BAPI_COMPANYCODE_GETLIST", SistemaSap.Erp);
        await Esperar(o, ct);

        return o.FallaSociedades
            ? new ResultadoSociedades(false, MensajeFalloSociedades, [])
            : new ResultadoSociedades(true, $"{SociedadesErp.Count} sociedades leídas.", SociedadesErp);
    }

    public async Task<ResultadoCuentas> LeerCuentasAsync(DestinoSap erp, string sociedad, CancellationToken ct = default)
    {
        var o = _opciones();
        Registrar("BAPI_GL_ACC_GETLIST", SistemaSap.Erp);
        await Esperar(o, ct);

        if (o.FallaCuentas)
        {
            return new ResultadoCuentas(false, MensajeFalloCuentas, "", "", "", []);
        }

        var soc = SociedadesErp.FirstOrDefault(s => s.Codigo == sociedad);
        if (soc is null)
        {
            return new ResultadoCuentas(false, $"La sociedad {sociedad} no existe en el ERP.", "", "", "", []);
        }

        // Los saldos de la filial salen de los de la matriz con un factor, para que la vista previa cambie por sociedad.
        var factor = soc.Codigo == "2000" ? 0.35m : 1m;
        var cuentas = Cuentas
            .Where(c => c.Plan == soc.PlanCuentas)
            .Select(c => new CuentaErp(c.Numero, c.Descripcion, c.Nodo, decimal.Round(c.Saldo * factor, 2)))
            .Concat(o.CuentasAdicionales.Where(c => c.Plan == soc.PlanCuentas).Select(c => new CuentaErp(c.Numero, c.Descripcion, c.Nodo, c.Saldo)))
            .OrderBy(c => c.Numero, StringComparer.Ordinal)
            .ToList();
        return new ResultadoCuentas(true, $"{cuentas.Count} cuentas del plan {soc.PlanCuentas}.", soc.PlanCuentas, soc.Moneda, PeriodoSaldos, cuentas);
    }

    /// <summary>Sociedades del ERP de ejemplo (prototipo F4). La 9000 no está: es la sociedad no SAP.</summary>
    public static readonly IReadOnlyList<SociedadErp> SociedadesErp =
    [
        new("1000", "Matriz", "COP", "PCCO"),
        new("2000", "Filial Colombia", "COP", "PCCO"),
        new("3000", "Filial Panamá", "USD", "PCPA"),
    ];

    /// <summary>Plan de cuentas de ejemplo. Saldos de la sociedad 1000 (o 3000 en PCPA); los créditos van negativos, como en SAP.</summary>
    public static readonly IReadOnlyList<CuentaSimulada> Cuentas =
    [
        C("PCCO", "11050501", "Caja general", "Disponible", 152_300_000m),
        C("PCCO", "11100501", "Bancos nacionales", "Disponible", 12_480_000_000m),
        C("PCCO", "11200501", "Cuentas de ahorro", "Disponible", 5_820_000_000m),
        C("PCCO", "11250501", "Inversiones a la vista", "Disponible", 1_250_000_000m),
        C("PCCO", "13050501", "Clientes nacionales", "Deudores", 31_420_900_000m),
        C("PCCO", "13300501", "Anticipos a proveedores", "Deudores", 2_310_000_000m),
        C("PCCO", "13550501", "Anticipo de impuestos", "Deudores", 1_870_000_000m),
        C("PCCO", "15040501", "Terrenos", "Propiedad, planta y equipo", 22_000_000_000m),
        C("PCCO", "15160501", "Construcciones y edificaciones", "Propiedad, planta y equipo", 58_400_000_000m),
        C("PCCO", "15240501", "Equipo de oficina", "Propiedad, planta y equipo", 3_950_000_000m),
        C("PCCO", "15920501", "Depreciación acumulada", "Propiedad, planta y equipo", -11_230_000_000m),
        C("PCCO", "21050501", "Obligaciones con bancos nacionales", "Obligaciones financieras", -42_100_000_000m),
        C("PCCO", "21200501", "Pagarés", "Obligaciones financieras", -13_300_000_000m),
        C("PCCO", "22050501", "Proveedores nacionales", "Proveedores", -24_780_450_000m),
        C("PCCO", "23350501", "Costos y gastos por pagar", "Cuentas por pagar", -7_000_000_000m),
        C("PCCO", "25050501", "Salarios por pagar", "Obligaciones laborales", -1_940_000_000m),
        C("PCCO", "31050501", "Capital suscrito y pagado", "Patrimonio", -40_000_000_000m),
        C("PCCO", "36050501", "Utilidad del ejercicio", "Patrimonio", -6_120_000_000m),
        C("PCCO", "41200501", "Ventas de productos", "Ingresos operacionales", -96_540_000_000m),
        C("PCCO", "41750501", "Devoluciones en ventas", "Ingresos operacionales", 1_430_000_000m),
        C("PCCO", "51050501", "Sueldos", "Gastos de administración", 9_100_000_000m),
        C("PCCO", "51350501", "Servicios públicos", "Gastos de administración", 1_200_000_000m),
        C("PCCO", "51400501", "Gastos legales", "Gastos de administración", 450_000_000m),
        C("PCCO", "52050501", "Sueldos de ventas", "Gastos de ventas", 6_800_000_000m),
        C("PCCO", "53050501", "Gastos financieros", "Gastos no operacionales", 4_210_000_000m),
        C("PCPA", "1010", "Caja", "Disponible", 85_000m),
        C("PCPA", "1020", "Bancos", "Disponible", 2_340_000m),
        C("PCPA", "1200", "Cuentas por cobrar comerciales", "Deudores", 4_120_500m),
        C("PCPA", "1500", "Propiedad, planta y equipo", "Propiedad, planta y equipo", 6_800_000m),
        C("PCPA", "2000", "Cuentas por pagar comerciales", "Proveedores", -3_150_000m),
        C("PCPA", "2100", "Préstamos bancarios", "Obligaciones financieras", -4_500_000m),
        C("PCPA", "3000", "Capital social", "Patrimonio", -5_000_000m),
        C("PCPA", "4000", "Ingresos por ventas", "Ingresos operacionales", -9_870_000m),
        C("PCPA", "5000", "Gastos de administración", "Gastos de administración", 1_640_000m),
    ];

    private static CuentaSimulada C(string plan, string numero, string descripcion, string nodo, decimal saldo) =>
        new() { Plan = plan, Numero = numero, Descripcion = descripcion, Nodo = nodo, Saldo = saldo };

    private static IReadOnlyList<ObjetoInventario> InfoProviders(bool bw4, bool bodega)
    {
        var tipo = bw4 ? "aDSO" : "InfoCubo";
        var lista = new List<ObjetoInventario>
        {
            new(bw4 ? "ZSD_D01" : "ZSD_C01", "Ventas por cliente y material (ejemplo)", true, tipo),
            new(bw4 ? "ZCO_D01" : "ZCO_C01", "Costos por centro de costo (ejemplo)", true, tipo),
        };
        if (bodega)
        {
            lista.Add(new(bw4 ? "ZACC_D01" : "ZACC_C01", "Saldos de contabilidad general (bodega financiera)", true, tipo, EsBodegaFinanciera: true));
            lista.Add(new(bw4 ? "ZACC_D02" : "ZACC_C02", "Partidas de contabilidad general (bodega financiera)", true, tipo, EsBodegaFinanciera: true));
        }

        return lista;
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
