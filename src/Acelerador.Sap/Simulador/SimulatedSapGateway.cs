using System.Collections.Concurrent;

namespace Acelerador.Sap.Simulador;

/// <summary>Una llamada recibida por el simulador. Sirve para comprobar que nada escribe en el cliente (RN-19).</summary>
public sealed record LlamadaSimulada(string Operacion, SistemaSap Sistema, bool EsEscritura);

/// <summary>Un objeto que el simulador creó en el cliente.</summary>
public sealed record ObjetoSimulado(SistemaSap Sistema, string Host, OperacionSap Operacion);

/// <summary>
/// Implementación de <see cref="ISapGateway"/> sin SAP: imita los paisajes de <see cref="PerfilSimulado"/>,
/// los fallos de conexión y los mensajes que devuelve SAP. Se usa en las pruebas unitarias y en
/// desarrollo mientras la prueba técnica HU-01 no confirme el conector real.
/// </summary>
public sealed partial class SimulatedSapGateway : ISapGateway
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
    private readonly ILogicaSimulada? _logica;
    private readonly AlmacenSimulado _almacen;
    private readonly ConcurrentQueue<LlamadaSimulada> _llamadas = new();
    private readonly ConcurrentDictionary<string, ObjetoSimulado> _objetos = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, OperacionSap> _datos = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Cargas de datos (p. ej. archivos no SAP en ZACC_NOSAP) por nombre técnico; una carga nueva reemplaza la anterior.</summary>
    public IReadOnlyDictionary<string, OperacionSap> Datos => _datos;

    /// <summary>Nombres técnicos de la bodega financiera que ya existe cuando BW la tiene (RN-03).</summary>
    public static readonly IReadOnlyList<string> ObjetosBodegaBw75 = ["ZACC_O01", "ZACC_C01", "ZACC_C02"];

    public static readonly IReadOnlyList<string> ObjetosBodegaBw4 = ["ZACC_D01", "ZACC_D02"];

    public static string MensajeSinEscritura(OperacionSap op, SistemaSap sistema) =>
        $"{op.Funcion}: el usuario técnico no tiene permiso de escritura en {NombreSistema(sistema)} (Z_ACC_PASO = CREAR).";

    public SimulatedSapGateway(OpcionesSimulador opciones, ILogicaSimulada? logica = null)
        : this(() => opciones, logica)
    {
    }

    /// <summary>
    /// Permite leer la configuración en cada llamada (cambios en caliente de appsettings). <paramref name="logica"/> es la
    /// lógica de BPC (cuadre, importación, conversión, eliminaciones) que se ejecuta con los scripts generados.
    /// </summary>
    public SimulatedSapGateway(Func<OpcionesSimulador> opciones, ILogicaSimulada? logica = null)
    {
        _opciones = opciones;
        _logica = logica;
        _almacen = new AlmacenSimulado(opciones, () => _datos);
    }

    /// <summary>Estado del cliente simulado: datos y validaciones de BPC.</summary>
    public IAlmacenSimulado Almacen => _almacen;

    /// <summary>Tasas de ejemplo frente al dólar (pesos por dólar) para todos los periodos.</summary>
    public static readonly IReadOnlyDictionary<(string Moneda, string Tipo), decimal> Tasas = new Dictionary<(string, string), decimal>
    {
        [("COP", "cierre")] = 4_000m,
        [("COP", "promedio")] = 3_950m,
        [("COP", "historica")] = 3_800m,
    };

    /// <summary>Partidas intercompañía de ejemplo, incluidas en los saldos (sociedad, cuenta, socio, importe en moneda local).</summary>
    public static readonly IReadOnlyList<(string Sociedad, string Cuenta, string Socio, decimal Importe)> Intercompania =
    [
        ("1000", "13050501", "2000", 1_000_000_000m),
        ("2000", "22050501", "1000", -1_000_000_000m),
        ("1000", "41200501", "2000", -400_000_000m),
        ("2000", "51050501", "1000", 400_000_000m),
        ("1000", "13050501", "3000", 200_000_000m),
        ("3000", "2000", "1000", -50_000m),
        ("1000", "12050501", "2000", 11_500_000_000m),
        ("1000", "12050501", "3000", 19_000_000_000m),
    ];

    /// <summary>Cuentas que solo tiene la matriz 1000 (la inversión en sus filiales y la reserva que la respalda).</summary>
    public static readonly IReadOnlySet<string> SoloMatriz = new HashSet<string> { "12050501", "33050501" };

    private static decimal Factor(string sociedad, string cuenta) =>
        SoloMatriz.Contains(cuenta) && sociedad != "1000" ? 0m : sociedad == "2000" ? 0.35m : 1m;

    /// <summary>Saldos de una sociedad del ERP al cierre del periodo, con las partidas intercompañía separadas por socio.</summary>
    public static IReadOnlyList<SaldoErp> SaldosErp(string sociedad, OpcionesSimulador opciones)
    {
        var soc = SociedadesErp.FirstOrDefault(s => s.Codigo == sociedad);
        if (soc is null)
        {
            return [];
        }

        var lista = new List<SaldoErp>();
        foreach (var c in Cuentas.Where(c => c.Plan == soc.PlanCuentas).Concat(opciones.CuentasAdicionales.Where(c => c.Plan == soc.PlanCuentas)))
        {
            var total = decimal.Round(c.Saldo * (opciones.CuentasAdicionales.Contains(c) ? 1m : Factor(sociedad, c.Numero)), 2);
            var ic = Intercompania.Where(i => i.Sociedad == sociedad && i.Cuenta == c.Numero).ToList();
            foreach (var i in ic)
            {
                lista.Add(new SaldoErp(c.Numero, i.Socio, i.Importe));
            }

            lista.Add(new SaldoErp(c.Numero, "", total - ic.Sum(i => i.Importe)));
        }

        foreach (var ajuste in opciones.AjustesSaldo.Where(a => a.StartsWith(sociedad + ":", StringComparison.Ordinal)))
        {
            var partes = ajuste.Split(':');
            lista.Add(new SaldoErp(partes[1], partes.Length > 3 ? partes[3] : "", decimal.Parse(partes[2], System.Globalization.CultureInfo.InvariantCulture)));
        }

        return lista;
    }

    public IReadOnlyCollection<LlamadaSimulada> Llamadas => _llamadas.ToArray();

    /// <summary>Objetos creados en el cliente simulado, en el orden en que se crearon.</summary>
    public IReadOnlyList<ObjetoSimulado> Objetos => [.. _objetos.Values];

    public async Task<ResultadoOperacionSap> EjecutarAsync(DestinoSap destino, OperacionSap operacion, CancellationToken ct = default)
    {
        var o = _opciones();
        _llamadas.Enqueue(new LlamadaSimulada($"{operacion.Funcion} {operacion.NombreTecnico}", destino.Sistema, EsEscritura: true));
        await Esperar(o, ct);
        if (o.LatenciaEscrituraMs > 0)
        {
            await Task.Delay(o.LatenciaEscrituraMs, ct);
        }

        if (o.FallaEscritura.Contains(destino.Sistema)
            || o.FallaObjetos.Contains(operacion.NombreTecnico, StringComparer.OrdinalIgnoreCase)
            || string.Equals(destino.Parametros.UsuarioTecnico, "RFC_LECTURA", StringComparison.OrdinalIgnoreCase))
        {
            return new ResultadoOperacionSap(false, MensajeSinEscritura(operacion, destino.Sistema));
        }

        if (operacion.TipoObjeto == "CARGA")
        {
            return CargarMaestro(o, operacion);
        }

        if (operacion.TipoObjeto is "VALIDACION" or "IMPORTACION" or "LOGICA" or "PARTICIPACIONES" or "ASIENTO" or "BLOQUEO" or "PRESUPUESTO")
        {
            if (o.FallaLogica.Contains(operacion.NombreTecnico, StringComparer.OrdinalIgnoreCase))
            {
                return new ResultadoOperacionSap(false, $"UJK: la lógica {operacion.NombreTecnico} terminó con error: la regla no está activa en el modelo.");
            }

            return _logica?.Ejecutar(operacion, _almacen, o) ?? new ResultadoOperacionSap(true, $"{operacion.NombreTecnico} ejecutado.", ResultadoEscritura.Creado);
        }

        if (operacion.TipoObjeto is "SCRIPT" or "PLANTILLA")
        {
            // Un script o una plantilla se reemplazan siempre (el paso respalda con UJBR antes, HU-28 CA4).
            var claveScript = $"{destino.Sistema}|{destino.Parametros.Host}|{operacion.TipoObjeto}|{operacion.NombreTecnico}";
            var existia = _objetos.ContainsKey(claveScript);
            _objetos[claveScript] = new ObjetoSimulado(destino.Sistema, destino.Parametros.Host, operacion);
            return new ResultadoOperacionSap(true, $"{operacion.NombreTecnico} {(existia ? "reemplazado" : "desplegado")} en {(operacion.TipoObjeto == "SCRIPT" ? "UJFS" : "la carpeta de plantillas de BPC")}.",
                existia ? ResultadoEscritura.Modificado : ResultadoEscritura.Creado);
        }

        if (destino.Sistema == SistemaSap.Bpc && operacion.TipoObjeto == "MIEMBROS")
        {
            var invalidos = Miembros(operacion).Where(m => !MiembroBpcValido().IsMatch(m)).ToList();
            if (invalidos.Count > 0)
            {
                return new ResultadoOperacionSap(false,
                    $"UJA_MEMBER: BPC rechazó el miembro «{invalidos[0]}» de {operacion.NombreTecnico}: solo admite letras, números, guion, punto y guion bajo (hasta 32).");
            }
        }

        if (operacion.TipoObjeto == "DATOS")
        {
            // Una carga de datos no es un objeto: se reemplaza completa (todo o nada) y nunca responde EXISTE.
            var reemplazo = _datos.ContainsKey(operacion.NombreTecnico);
            _datos[operacion.NombreTecnico] = operacion;
            return new ResultadoOperacionSap(true, $"{operacion.NombreTecnico}: datos {(reemplazo ? "reemplazados" : "cargados")}.",
                reemplazo ? ResultadoEscritura.Modificado : ResultadoEscritura.Creado);
        }

        var bodega = o.BodegaExistente || Contiene(destino.Parametros.Host, "bodega");
        var preexistente = destino.Sistema == SistemaSap.Bw && bodega
            && (ObjetosBodegaBw75.Contains(operacion.NombreTecnico) || ObjetosBodegaBw4.Contains(operacion.NombreTecnico));
        var clave = $"{destino.Sistema}|{destino.Parametros.Host}|{operacion.TipoObjeto}|{operacion.NombreTecnico}";
        if (preexistente || !_objetos.TryAdd(clave, new ObjetoSimulado(destino.Sistema, destino.Parametros.Host, operacion)))
        {
            return new ResultadoOperacionSap(true, $"{operacion.NombreTecnico}: EXISTE, no se modifica.", ResultadoEscritura.Existente);
        }

        if (operacion.TipoObjeto == "MIEMBROS")
        {
            var n = Miembros(operacion).Count();
            return new ResultadoOperacionSap(true, $"{operacion.NombreTecnico}: {n} miembros creados.", ResultadoEscritura.Creado, Cantidad: n);
        }

        return new ResultadoOperacionSap(true, $"{operacion.NombreTecnico} creado.", ResultadoEscritura.Creado);
    }

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

    public async Task<ResultadoConsultaBpc> ConsultarBpcAsync(DestinoSap bpc, ConsultaBpc consulta, CancellationToken ct = default)
    {
        var o = _opciones();
        Registrar("Z_ACC_BPC_LEER", SistemaSap.Bpc);
        await Esperar(o, ct);
        if (o.SistemasConFallo.Contains(SistemaSap.Bpc) || Contiene(bpc.Parametros.Host, "falla"))
        {
            return new ResultadoConsultaBpc(false, MensajeNoAlcanzable(bpc.Parametros.Host), [], []);
        }

        var registros = _almacen.Registros.Where(r =>
                r.Modelo == consulta.Modelo
                && (consulta.Periodo is null || r.Periodo == consulta.Periodo)
                && (consulta.Moneda is null || r.Moneda == consulta.Moneda)
                && (consulta.Origen is null || r.Origen == consulta.Origen)
                && (consulta.Entidad is null || r.Entidad == consulta.Entidad))
            .ToList();
        var validaciones = _almacen.Validaciones.Where(v => consulta.Periodo is null || v.Periodo.Replace('-', '.') == consulta.Periodo.Replace('-', '.')).ToList();
        var auditoria = _almacen.Auditoria.Where(a => a.Modelo == consulta.Modelo).ToList();
        bool DelPeriodo(string periodo) => consulta.Periodo is null || periodo.Replace('-', '.') == consulta.Periodo.Replace('-', '.');
        var asientos = _almacen.Asientos.Where(a => DelPeriodo(a.Periodo)).ToList();
        var bloqueos = _almacen.Bloqueos.Where(b => DelPeriodo(b.Periodo)).ToList();
        return new ResultadoConsultaBpc(true, $"{registros.Count} registros.", registros, validaciones, auditoria, asientos, bloqueos);
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
                new("ZZREGION", "Región comercial (ejemplo)", true, s4 ? "ACDOCA" : "BSEG", TipoDato: "CHAR 4"),
                new("ZZLINEA", "Línea de negocio (ejemplo)", true, s4 ? "ACDOCA" : "BSEG", TipoDato: "CHAR 10"),
                new("ZZNOTAS", "Notas libres del asiento (ejemplo)", true, s4 ? "ACDOCA" : "BSEG", TipoDato: "STRG"),
                .. o.CamposZAdicionales.Select(c => new ObjetoInventario(c, "Campo agregado por el cliente", true, s4 ? "ACDOCA" : "BSEG", TipoDato: "CHAR 10")),
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
        var cuentas = Cuentas
            .Where(c => c.Plan == soc.PlanCuentas)
            .Select(c => new CuentaErp(c.Numero, c.Descripcion, c.Nodo, decimal.Round(c.Saldo * Factor(soc.Codigo, c.Numero), 2)))
            .Concat(o.CuentasAdicionales.Where(c => c.Plan == soc.PlanCuentas).Select(c => new CuentaErp(c.Numero, c.Descripcion, c.Nodo, c.Saldo)))
            .OrderBy(c => c.Numero, StringComparer.Ordinal)
            .ToList();
        return new ResultadoCuentas(true, $"{cuentas.Count} cuentas del plan {soc.PlanCuentas}.", soc.PlanCuentas, soc.Moneda, PeriodoSaldos, cuentas);
    }

    /// <summary>Centros de costo y de beneficio de cada sociedad del ERP de ejemplo.</summary>
    public static readonly IReadOnlyDictionary<string, (int CentrosCosto, int CentrosBeneficio)> Centros =
        new Dictionary<string, (int, int)> { ["1000"] = (12, 5), ["2000"] = (8, 3), ["3000"] = (4, 2) };

    /// <summary>
    /// Cargas de datos maestros de BW (HU-21 a HU-24): el paquete Z lee del ERP y carga en BW. Devuelve la cantidad
    /// cargada; los faltantes configurados son errores y las cuentas huérfanas de la jerarquía, avisos.
    /// </summary>
    private ResultadoOperacionSap CargarMaestro(OpcionesSimulador o, OperacionSap op)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(op.Definicion);
        var d = doc.RootElement;
        string Texto(string p) => d.TryGetProperty(p, out var v) ? v.GetString() ?? "" : "";
        var sociedades = d.TryGetProperty("sociedades", out var s) ? s.EnumerateArray().Select(x => x.GetString()!).ToList() : [];
        switch (Texto("tipo"))
        {
            case "PLAN_CUENTAS":
            case "JERARQUIA":
            {
                var plan = Texto("plan");
                if (o.PlanesSinDatos.Contains(plan) || !Cuentas.Any(c => c.Plan == plan))
                {
                    return new ResultadoOperacionSap(false, $"{string.Join(", ", sociedades)}: el plan de cuentas {plan} no existe en el ERP (SKA1).");
                }

                var cuentas = Cuentas.Where(c => c.Plan == plan).Concat(o.CuentasAdicionales.Where(c => c.Plan == plan)).ToList();
                if (Texto("tipo") == "PLAN_CUENTAS")
                {
                    return new ResultadoOperacionSap(true, $"{plan}: {cuentas.Count} cuentas cargadas.", ResultadoEscritura.Creado, Cantidad: cuentas.Count);
                }

                var huerfanas = o.CuentasHuerfanasJerarquia.Where(x => x.StartsWith(plan + ":", StringComparison.Ordinal)).Select(x => x[(plan.Length + 1)..]).ToList();
                var nodos = 1 + cuentas.Select(c => c.Nodo).Distinct().Count() + cuentas.Count;
                return new ResultadoOperacionSap(true, $"{plan}: {nodos} nodos cargados.", ResultadoEscritura.Creado, Cantidad: nodos,
                    Aviso: huerfanas.Count == 0 ? null : $"{plan}: la jerarquía tiene {string.Join(", ", huerfanas)}, que no {(huerfanas.Count == 1 ? "existe" : "existen")} en el plan de cuentas; no se {(huerfanas.Count == 1 ? "carga" : "cargan")}.");
            }

            case "CENTROS":
            {
                var soc = sociedades.FirstOrDefault() ?? "";
                if (o.FallaCentrosSociedades.Contains(soc))
                {
                    return new ResultadoOperacionSap(false, $"{soc}: el área de controlling asignada a la sociedad no existe (OKKP).");
                }

                var (ceco, cebe) = Centros.GetValueOrDefault(soc, (0, 0));
                return new ResultadoOperacionSap(true, $"{soc}: {ceco} centros de costo y {cebe} centros de beneficio cargados.", ResultadoEscritura.Creado, Cantidad: ceco + cebe);
            }

            case "TASAS":
            {
                var moneda = Texto("moneda");
                var periodos = d.GetProperty("periodos").EnumerateArray().Select(x => x.GetString()!).ToList();
                var falta = periodos.FirstOrDefault(p => o.TasasFaltantes.Contains($"{moneda}:{p}"));
                if (falta is not null)
                {
                    return new ResultadoOperacionSap(false, $"No hay tasa de cierre {moneda}/USD para {falta} en el ERP (TCURR).");
                }

                var tipos = d.GetProperty("tipos").EnumerateObject().Count();
                return new ResultadoOperacionSap(true, $"{moneda}/USD: {periodos.Count * tipos} tasas cargadas ({periodos.Count} periodos × {tipos} tipos).",
                    ResultadoEscritura.Creado, Cantidad: periodos.Count * tipos);
            }

            case "MONEDAS":
                return new ResultadoOperacionSap(true, $"{sociedades.Count} monedas locales cargadas desde T001.", ResultadoEscritura.Creado, Cantidad: sociedades.Count);

            case "MIEMBROS_BW":
            {
                var cantidad = sociedades.Sum(x => Centros.GetValueOrDefault(x, (0, 0)) is var (cc, pc) ? cc + pc : 0);
                return new ResultadoOperacionSap(true, $"{cantidad} centros cargados como miembros de {Texto("dimension")}.", ResultadoEscritura.Creado, Cantidad: cantidad);
            }

            default:
                return new ResultadoOperacionSap(true, $"{op.NombreTecnico} cargado.", ResultadoEscritura.Creado);
        }
    }

    private static IEnumerable<string> Miembros(OperacionSap op)
    {
        using var doc = System.Text.Json.JsonDocument.Parse(op.Definicion);
        return doc.RootElement.TryGetProperty("miembros", out var m)
            ? m.EnumerateArray().Select(x => x.GetProperty("id").GetString()!).ToList()
            : [];
    }

    [System.Text.RegularExpressions.GeneratedRegex("^[A-Za-z0-9_.-]{1,32}$")]
    private static partial System.Text.RegularExpressions.Regex MiembroBpcValido();

    /// <summary>Sociedades del ERP de ejemplo (prototipo F4). La 9000 no está: es la sociedad no SAP.</summary>
    public static readonly IReadOnlyList<SociedadErp> SociedadesErp =
    [
        new("1000", "Matriz", "COP", "PCCO"),
        new("2000", "Filial Colombia", "COP", "PCCO"),
        new("3000", "Filial Panamá", "USD", "PCPA"),
    ];

    /// <summary>
    /// Plan de cuentas de ejemplo. Saldos de la sociedad 1000 (o 3000 en PCPA); los créditos van negativos, como en SAP.
    /// Cuadran como pide RN-16: las cuentas de balance suman cero y la utilidad del ejercicio es la suma de resultados.
    /// </summary>
    public static readonly IReadOnlyList<CuentaSimulada> Cuentas =
    [
        C("PCCO", "11050501", "Caja general", "Disponible", 152_300_000m),
        C("PCCO", "11100501", "Bancos nacionales", "Disponible", 12_480_000_000m),
        C("PCCO", "11200501", "Cuentas de ahorro", "Disponible", 5_820_000_000m),
        C("PCCO", "11250501", "Inversiones a la vista", "Disponible", 1_250_000_000m),
        C("PCCO", "13050501", "Clientes nacionales", "Deudores", 31_420_900_000m),
        C("PCCO", "13300501", "Anticipos a proveedores", "Deudores", 2_310_000_000m),
        C("PCCO", "13550501", "Anticipo de impuestos", "Deudores", 1_870_000_000m),
        C("PCCO", "12050501", "Inversiones en subsidiarias", "Inversiones", 30_500_000_000m),
        C("PCCO", "15040501", "Terrenos", "Propiedad, planta y equipo", 22_000_000_000m),
        C("PCCO", "15160501", "Construcciones y edificaciones", "Propiedad, planta y equipo", 72_447_250_000m),
        C("PCCO", "15240501", "Equipo de oficina", "Propiedad, planta y equipo", 3_950_000_000m),
        C("PCCO", "15920501", "Depreciación acumulada", "Propiedad, planta y equipo", -11_230_000_000m),
        C("PCCO", "21050501", "Obligaciones con bancos nacionales", "Obligaciones financieras", -42_100_000_000m),
        C("PCCO", "21200501", "Pagarés", "Obligaciones financieras", -13_300_000_000m),
        C("PCCO", "22050501", "Proveedores nacionales", "Proveedores", -24_780_450_000m),
        C("PCCO", "23350501", "Costos y gastos por pagar", "Cuentas por pagar", -7_000_000_000m),
        C("PCCO", "25050501", "Salarios por pagar", "Obligaciones laborales", -1_940_000_000m),
        C("PCCO", "31050501", "Capital suscrito y pagado", "Patrimonio", -40_000_000_000m),
        C("PCCO", "33050501", "Reservas", "Patrimonio", -30_500_000_000m),
        C("PCCO", "36050501", "Utilidad del ejercicio", "Patrimonio", -13_350_000_000m),
        C("PCCO", "41200501", "Ventas de productos", "Ingresos operacionales", -36_540_000_000m),
        C("PCCO", "41750501", "Devoluciones en ventas", "Ingresos operacionales", 1_430_000_000m),
        C("PCCO", "51050501", "Sueldos", "Gastos de administración", 9_100_000_000m),
        C("PCCO", "51350501", "Servicios públicos", "Gastos de administración", 1_200_000_000m),
        C("PCCO", "51400501", "Gastos legales", "Gastos de administración", 450_000_000m),
        C("PCCO", "52050501", "Sueldos de ventas", "Gastos de ventas", 6_800_000_000m),
        C("PCCO", "53050501", "Gastos financieros", "Gastos no operacionales", 4_210_000_000m),
        C("PCPA", "1010", "Caja", "Disponible", 85_000m),
        C("PCPA", "1020", "Bancos", "Disponible", 2_340_000m),
        C("PCPA", "1200", "Cuentas por cobrar comerciales", "Deudores", 4_120_500m),
        C("PCPA", "1500", "Propiedad, planta y equipo", "Propiedad, planta y equipo", 14_334_500m),
        C("PCPA", "2000", "Cuentas por pagar comerciales", "Proveedores", -3_150_000m),
        C("PCPA", "2100", "Préstamos bancarios", "Obligaciones financieras", -4_500_000m),
        C("PCPA", "3000", "Capital social", "Patrimonio", -5_000_000m),
        C("PCPA", "3100", "Utilidad del ejercicio", "Patrimonio", -8_230_000m),
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
        if (bodega && bw4)
        {
            lista.Add(new("ZACC_D01", "Partidas de contabilidad general (bodega financiera)", true, tipo, EsBodegaFinanciera: true));
            lista.Add(new("ZACC_D02", "Saldos de contabilidad general (bodega financiera)", true, tipo, EsBodegaFinanciera: true));
        }
        else if (bodega)
        {
            lista.Add(new("ZACC_O01", "Partidas de contabilidad general (bodega financiera)", true, "DSO", EsBodegaFinanciera: true));
            lista.Add(new("ZACC_C01", "Saldos de contabilidad general (bodega financiera)", true, tipo, EsBodegaFinanciera: true));
            lista.Add(new("ZACC_C02", "Partidas de contabilidad general (bodega financiera)", true, tipo, EsBodegaFinanciera: true));
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

    private static string NombreSistema(SistemaSap sistema) => sistema switch
    {
        SistemaSap.Erp => "SAP ERP",
        SistemaSap.Bw => "SAP BW",
        _ => "SAP BPC",
    };

    private void Registrar(string operacion, SistemaSap sistema) =>
        _llamadas.Enqueue(new LlamadaSimulada(operacion, sistema, EsEscritura: false));

    private static Task Esperar(OpcionesSimulador o, CancellationToken ct) =>
        o.LatenciaMs > 0 ? Task.Delay(o.LatenciaMs, ct) : Task.CompletedTask;

    private static bool Contiene(string texto, string valor) =>
        texto.Contains(valor, StringComparison.OrdinalIgnoreCase);
}
