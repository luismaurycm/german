using System.Text.Json;

namespace Acelerador.Sap.Simulador;

/// <summary>Saldo de una cuenta del ERP o del archivo no SAP, con la sociedad asociada si es intercompañía.</summary>
public sealed record SaldoErp(string Cuenta, string Socio, decimal Importe);

/// <summary>Lo que el simulador sabe del cliente: saldos del ERP, cargas no SAP, tasas, datos y validaciones de BPC.</summary>
public interface IAlmacenSimulado
{
    IReadOnlyList<SaldoErp> SaldosErp(string sociedad);

    IReadOnlyList<SaldoErp> DatosNoSap(string sociedad, string periodo);

    /// <summary>Presupuesto y ejecución del ERP por cuenta, centro y mes (0FI_GL_14 en BW) para el modelo de planeación.</summary>
    IReadOnlyList<SaldoPlan> PlanErp(string sociedad, int anio);

    /// <summary>Tasa de la moneda frente al dólar (unidades de moneda por dólar) para el tipo y periodo, o null si falta.</summary>
    decimal? Tasa(string moneda, string tipo, string periodo);

    IReadOnlyList<RegistroBpc> Registros { get; }

    /// <summary>Quita los registros que cumplen la condición y agrega los nuevos, en una sola operación (todo o nada).</summary>
    void Reemplazar(Func<RegistroBpc, bool> quitar, IEnumerable<RegistroBpc> nuevos);

    IReadOnlyList<ValidacionCuadre> Validaciones { get; }

    void GuardarValidacion(ValidacionCuadre validacion);

    /// <summary>Auditoría estándar de BPC (RNF-Aud-02).</summary>
    IReadOnlyList<AuditoriaBpc> Auditoria { get; }

    void Auditar(IEnumerable<AuditoriaBpc> cambios);

    /// <summary>Asientos de ajuste contabilizados (RF-35).</summary>
    IReadOnlyList<AsientoBpc> Asientos { get; }

    /// <summary>Contabiliza el asiento con el número siguiente (AJ-0001, AJ-0002…) y lo devuelve.</summary>
    AsientoBpc Contabilizar(Func<string, AsientoBpc> asiento);

    /// <summary>Estados de trabajo por sociedad y periodo (RN-17).</summary>
    IReadOnlyList<BloqueoBpc> Bloqueos { get; }

    void GuardarBloqueo(BloqueoBpc bloqueo);

    /// <summary>Si la sociedad está bloqueada en el periodo (formato BPC «2026.09» o «2026-09»).</summary>
    bool Bloqueada(string sociedad, string periodo);
}

/// <summary>
/// La lógica que BPC ejecuta con los scripts generados (validación de cuadre, importación, conversión, eliminaciones).
/// El simulador no la conoce: se la inyecta el motor de referencia de Core, que es la especificación de esos scripts.
/// </summary>
public interface ILogicaSimulada
{
    /// <summary>Ejecuta una operación de lógica (tipo VALIDACION, IMPORTACION o LOGICA); null si no la reconoce.</summary>
    ResultadoOperacionSap? Ejecutar(OperacionSap operacion, IAlmacenSimulado almacen, OpcionesSimulador opciones);
}

/// <summary>Estado del cliente simulado.</summary>
internal sealed class AlmacenSimulado(Func<OpcionesSimulador> opciones, Func<IReadOnlyDictionary<string, OperacionSap>> datos) : IAlmacenSimulado
{
    private readonly object _candado = new();
    private List<RegistroBpc> _registros = [];
    private readonly Dictionary<(string, string), ValidacionCuadre> _validaciones = [];
    private readonly List<AuditoriaBpc> _auditoria = [];
    private readonly List<AsientoBpc> _asientos = [];
    private readonly Dictionary<(string, string), BloqueoBpc> _bloqueos = [];

    public IReadOnlyList<AsientoBpc> Asientos
    {
        get
        {
            lock (_candado)
            {
                return [.. _asientos];
            }
        }
    }

    public AsientoBpc Contabilizar(Func<string, AsientoBpc> asiento)
    {
        lock (_candado)
        {
            var nuevo = asiento($"AJ-{_asientos.Count + 1:0000}");
            _asientos.Add(nuevo);
            return nuevo;
        }
    }

    public IReadOnlyList<BloqueoBpc> Bloqueos
    {
        get
        {
            lock (_candado)
            {
                return [.. _bloqueos.Values];
            }
        }
    }

    public void GuardarBloqueo(BloqueoBpc bloqueo)
    {
        lock (_candado)
        {
            _bloqueos[(bloqueo.Sociedad, bloqueo.Periodo.Replace('-', '.'))] = bloqueo;
        }
    }

    public bool Bloqueada(string sociedad, string periodo)
    {
        lock (_candado)
        {
            return _bloqueos.TryGetValue((sociedad, periodo.Replace('-', '.')), out var b) && b.Bloqueada;
        }
    }

    public IReadOnlyList<AuditoriaBpc> Auditoria
    {
        get
        {
            lock (_candado)
            {
                return [.. _auditoria];
            }
        }
    }

    public void Auditar(IEnumerable<AuditoriaBpc> cambios)
    {
        lock (_candado)
        {
            _auditoria.AddRange(cambios);
        }
    }

    public IReadOnlyList<SaldoErp> SaldosErp(string sociedad) => SimulatedSapGateway.SaldosErp(sociedad, opciones());

    public IReadOnlyList<SaldoPlan> PlanErp(string sociedad, int anio) => SimulatedSapGateway.PlanErp(sociedad, anio, opciones());

    public IReadOnlyList<SaldoErp> DatosNoSap(string sociedad, string periodo)
    {
        if (!datos().TryGetValue($"ZACC_NOSAP/{sociedad}", out var op))
        {
            return [];
        }

        using var doc = JsonDocument.Parse(op.Definicion);
        return doc.RootElement.GetProperty("filas").EnumerateArray()
            .Where(f => f.GetProperty("periodo").GetString() == periodo)
            .Select(f => new SaldoErp(f.GetProperty("cuenta").GetString()!, f.GetProperty("tercero").GetString() ?? "", f.GetProperty("importe").GetDecimal()))
            .ToList();
    }

    public decimal? Tasa(string moneda, string tipo, string periodo) =>
        moneda == "USD" ? 1m
        : opciones().TasasFaltantes.Contains($"{moneda}:{periodo}") ? null
        : SimulatedSapGateway.Tasas.TryGetValue((moneda, tipo), out var t) ? t : null;

    public IReadOnlyList<RegistroBpc> Registros
    {
        get
        {
            lock (_candado)
            {
                return _registros;
            }
        }
    }

    public void Reemplazar(Func<RegistroBpc, bool> quitar, IEnumerable<RegistroBpc> nuevos)
    {
        var lista = nuevos.ToList();
        lock (_candado)
        {
            _registros = [.. _registros.Where(r => !quitar(r)), .. lista];
        }
    }

    public IReadOnlyList<ValidacionCuadre> Validaciones
    {
        get
        {
            lock (_candado)
            {
                return [.. _validaciones.Values];
            }
        }
    }

    public void GuardarValidacion(ValidacionCuadre validacion)
    {
        lock (_candado)
        {
            _validaciones[(validacion.Sociedad, validacion.Periodo)] = validacion;
        }
    }
}
