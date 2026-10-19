using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Servicios;

/// <summary>Lo común de las hojas del cierre (C01, C03, C04, asientos): el cliente, sus sociedades, el grupo y el periodo.</summary>
public sealed record ContextoCierre(Cliente Cliente, IReadOnlyList<Sociedad> Sociedades, string Grupo, string Periodo, IReadOnlyList<string> Periodos)
{
    public string PeriodoBpc => MotorConsolidacion.PeriodoBpc(Periodo);

    public IReadOnlyList<string> Monedas => Sociedades.Count == 0 ? [] : ReglasCierre.MonedasPresentacion(Sociedades);
}

/// <summary>Lectura del contexto y de las cuentas del cliente que comparten los servicios del cierre.</summary>
public sealed class ContextoCierreLector(IClienteRepository clientes, ISociedadRepository sociedades, IMapeoRepository mapeos, TimeProvider reloj)
{
    public async Task<ContextoCierre?> LeerAsync(Guid clienteId, string? periodo, CancellationToken ct)
    {
        var cliente = await clientes.ObtenerAsync(clienteId, ct);
        if (cliente is null)
        {
            return null;
        }

        var lista = await sociedades.ListarAsync(clienteId, ct);
        var periodos = ServicioParticipaciones.Periodos(reloj.GetUtcNow());
        var elegido = periodo is not null && periodos.Contains(periodo) ? periodo : ReglasCierre.PeriodoInicial(reloj.GetUtcNow());
        return new ContextoCierre(cliente, lista, lista.Count == 0 ? "" : ReglasCierre.Grupo(lista), elegido, periodos);
    }

    /// <summary>Las asignaciones de cuenta a rubro de todos los planes de las sociedades (HU-12).</summary>
    public async Task<IReadOnlyList<AsignacionCuenta>> AsignacionesAsync(Guid clienteId, IReadOnlyList<Sociedad> lista, CancellationToken ct)
    {
        var todas = new List<AsignacionCuenta>();
        foreach (var plan in lista.Select(s => s.PlanCuentas).Where(p => p.Length > 0).Distinct())
        {
            todas.AddRange(await mapeos.ListarAsync(clienteId, plan, ct));
        }

        return todas;
    }
}

public sealed record FilaC03(Sociedad Sociedad, ValidacionCuadre Validacion);

public sealed record VistaC03(ContextoCierre Contexto, IReadOnlyList<FilaC03> Filas, string? Error)
{
    public bool Vacio => Error is null && Filas.Count == 0;
}

public sealed record VistaC01(ContextoCierre Contexto, string Moneda, IReadOnlyList<ParejaIntercompania> Parejas, decimal Umbral, string? Error)
{
    public bool Vacio => Error is null && Parejas.Count == 0;
}

/// <summary>
/// HU-37 y HU-38 (UX-11): las hojas de control C03 Validación de cuadre y C01 Conciliación intercompañía, que leen BPC como
/// lo haría EPM. C01 lee los datos importados, así que se puede consultar antes de la consolidación del periodo.
/// </summary>
public sealed class ServicioControles(ContextoCierreLector contexto, AccesoSap acceso, ISapGateway sap)
{
    public const string SinImportaciones = "Aún no hay importaciones en el periodo elegido.";
    public const string SinIntercompania = "No hay saldos intercompañía en el periodo elegido.";

    public async Task<VistaC03?> C03Async(Guid clienteId, string? periodo, CancellationToken ct = default)
    {
        var c = await contexto.LeerAsync(clienteId, periodo, ct);
        if (c is null)
        {
            return null;
        }

        var (datos, error) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(MotorConsolidacion.Modelo, c.PeriodoBpc, Origen: LecturaBpc.SinRegistros), ct);
        var filas = (datos?.Validaciones ?? [])
            .Select(v => (Sociedad: c.Sociedades.FirstOrDefault(s => s.Codigo == v.Sociedad), v))
            .Where(x => x.Sociedad is not null)
            .Select(x => new FilaC03(x.Sociedad!, x.v))
            .OrderBy(f => f.Sociedad.Codigo, StringComparer.Ordinal)
            .ToList();
        return new VistaC03(c, filas, error);
    }

    public async Task<VistaC01?> C01Async(Guid clienteId, string? periodo, string? moneda, CancellationToken ct = default)
    {
        var c = await contexto.LeerAsync(clienteId, periodo, ct);
        if (c is null)
        {
            return null;
        }

        var monedaElegida = moneda is not null && c.Monedas.Contains(moneda) ? moneda : c.Monedas.FirstOrDefault() ?? "";
        var (datos, error) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(MotorConsolidacion.Modelo, c.PeriodoBpc, monedaElegida, MotorConsolidacion.OrigenCarga), ct);
        if (datos is null)
        {
            return new VistaC01(c, monedaElegida, [], ReglasConciliacion.UmbralProvisional, error);
        }

        var parejas = await ParejasAsync(c, datos, monedaElegida, ct);
        return new VistaC01(c, monedaElegida, parejas, ReglasConciliacion.UmbralProvisional, null);
    }

    /// <summary>Las parejas de C01 con las sociedades vigentes en el modelo de propiedad (RN-07, RN-14).</summary>
    internal async Task<IReadOnlyList<ParejaIntercompania>> ParejasAsync(ContextoCierre c, ResultadoConsultaBpc datos, string moneda, CancellationToken ct)
    {
        var asignaciones = await contexto.AsignacionesAsync(c.Cliente.Id, c.Sociedades, ct);
        var clases = ReglasRubros.ClasesPorMiembro(asignaciones);
        var inversiones = asignaciones.Where(a => a.Rubro == ReglasRubros.RubroInversiones).Select(a => GeneradorModeloBpc.IdCuenta(a.PlanCuentas, a.Cuenta)).ToHashSet();
        var (propiedad, _) = await LecturaBpc.LeerAsync(acceso, sap, c.Cliente.Id, new ConsultaBpc(ModeloPropiedad.Modelo), ct);
        var vigentes = ModeloPropiedad.Vigentes(propiedad?.Registros ?? [], c.Grupo, c.PeriodoBpc).Participaciones;
        var grupo = vigentes.Count > 0
            ? vigentes.Where(p => p.Metodo != MetodoConsolidacion.FueraDelPerimetro).Select(p => p.Sociedad).ToHashSet()
            : ReglasCierre.Participantes(c.Sociedades).Select(s => s.Codigo).ToHashSet();
        return ReglasConciliacion.Parejas(datos.Registros, grupo, clases, inversiones, moneda);
    }
}

public sealed record FilaC04(Sociedad Sociedad, IReadOnlyList<CeldaCierre> Pasos, bool Bloqueada);

public sealed record VistaC04(ContextoCierre Contexto, IReadOnlyList<FilaC04> Filas, bool Iniciado, string? Aviso, string? Error);

/// <summary>
/// HU-40 y HU-41 (UX-15): C04 Estado del cierre por sociedad y los estados de trabajo de BPC para bloquear o desbloquear una
/// sociedad en un periodo sin afectar a las demás (RN-17), con la auditoría de BPC y el registro del acelerador.
/// </summary>
public sealed class ServicioCierre(
    ContextoCierreLector contexto,
    ServicioControles controles,
    IEjecucionPlanRepository ejecuciones,
    IReporteRepository reportes,
    AccesoSap acceso,
    ISapGateway sap,
    IRegistroEjecuciones registro,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string PasoBloquear = "Bloquear sociedad";
    public const string PasoDesbloquear = "Desbloquear sociedad";

    public async Task<VistaC04?> ObtenerAsync(Guid clienteId, string? periodo, CancellationToken ct = default)
    {
        var c = await contexto.LeerAsync(clienteId, periodo, ct);
        if (c is null)
        {
            return null;
        }

        var (datos, error) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(MotorConsolidacion.Modelo, c.PeriodoBpc), ct);
        if (datos is null)
        {
            return new VistaC04(c, [], false, null, error);
        }

        var monedaGrupo = c.Monedas.FirstOrDefault() ?? "";
        var parejas = await controles.ParejasAsync(c, datos, monedaGrupo, ct);
        var conDiferencia = parejas.Where(p => ReglasConciliacion.SuperaUmbral(p, ReglasConciliacion.UmbralProvisional)).SelectMany(p => new[] { p.Sociedad, p.Contraparte }).ToHashSet();
        var bloqueadas = ReglasCierreMensual.Bloqueadas(datos.Bloqueos ?? []);
        var consolidado = datos.Registros.Any(r => r.Grupo == c.Grupo && r.Origen is MotorConsolidacion.OrigenEliminacionIc or MotorConsolidacion.OrigenEliminacionInversion or MotorConsolidacion.OrigenParticipacion);
        var ultima = await ejecuciones.ObtenerUltimaAsync(clienteId, ct);
        var pasoConError = ultima is not null && ReglasCierre.PeriodoInicial(ultima.Inicio) == c.Periodo ? ultima.PasoConError?.Id : null;
        var (propiedad, _) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(ModeloPropiedad.Modelo), ct);
        var vigentes = ModeloPropiedad.Vigentes(propiedad?.Registros ?? [], c.Grupo, c.PeriodoBpc).Participaciones;
        var reportesGenerados = ServicioReportes.CierreGenerado(await reportes.ListarAsync(clienteId, ct));

        var filas = c.Sociedades.Select(s =>
        {
            var validacion = datos.Validaciones.FirstOrDefault(v => v.Sociedad == s.Codigo);
            var convertida = datos.Registros.Any(r => r.Entidad == s.Codigo && r.Grupo.Length == 0 && r.Moneda == monedaGrupo && r.Origen == MotorConsolidacion.OrigenCarga);
            var metodo = vigentes.FirstOrDefault(p => p.Sociedad == s.Codigo)?.Metodo ?? s.Metodo;
            var hechos = new HechosCierre(metodo, validacion, conDiferencia.Contains(s.Codigo), convertida, consolidado, pasoConError, ReportesGenerados: reportesGenerados);
            return new FilaC04(s, ReglasCierreMensual.Fila(hechos), bloqueadas.Contains(s.Codigo));
        }).ToList();

        var iniciado = datos.Validaciones.Count > 0 || datos.Registros.Count > 0;
        var sinCuadre = datos.Validaciones.Where(v => !v.Importable).Select(v => v.Sociedad).Order(StringComparer.Ordinal).ToList();
        return new VistaC04(c, filas, iniciado, ReglasCierreMensual.AvisoCuadre(sinCuadre), null);
    }

    /// <summary>Bloquear o Desbloquear (HU-40 CA1, CA3, CA4): solo esa sociedad y periodo, auditado en BPC con usuario y fecha.</summary>
    public async Task<ResultadoOperacion> CambiarBloqueoAsync(Guid clienteId, string periodo, string sociedad, bool bloquear, CancellationToken ct = default)
    {
        var c = await contexto.LeerAsync(clienteId, periodo, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        if (c.Sociedades.All(s => s.Codigo != sociedad))
        {
            return new ResultadoOperacion(false, $"{sociedad} no es una sociedad del grupo.");
        }

        var destino = await acceso.DestinoAsync(clienteId, SistemaSap.Bpc, ct);
        ResultadoOperacionSap r;
        if (destino.Destino is null)
        {
            r = new ResultadoOperacionSap(false, destino.Error!);
        }
        else
        {
            var definicion = Definicion.Json(new { logica = "BLOQUEO", modelo = MotorConsolidacion.Modelo, sociedad, periodo = c.Periodo, bloquear, usuario = usuario.Nombre, fecha = reloj.GetUtcNow() });
            try
            {
                r = await sap.EjecutarAsync(destino.Destino, new OperacionSap(FuncionesBpc.EstadoTrabajo, "BLOQUEO", $"{sociedad} {c.Periodo}", definicion), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                r = new ResultadoOperacionSap(false, $"Error inesperado al llamar a SAP: {ex.Message}");
            }
        }

        var mensaje = SanitizadorMensajes.Limpiar(r.Mensaje, destino.Credencial);
        await registro.RegistrarAsync(new Ejecucion(reloj.GetUtcNow(), usuario.Nombre, clienteId, Textos.Sistema(SistemaSap.Bpc), bloquear ? PasoBloquear : PasoDesbloquear,
            $"{sociedad} {c.Periodo}", r.Exitosa ? ResultadoEjecucion.Exitoso : ResultadoEjecucion.Fallido, mensaje), ct);
        return r.Exitosa
            ? new ResultadoOperacion(true, bloquear
                ? $"{sociedad} quedó bloqueada en {c.Periodo}: no admite cargas, asientos ni reprocesos del periodo (RN-17)."
                : $"{sociedad} quedó desbloqueada en {c.Periodo}.")
            : new ResultadoOperacion(false, mensaje);
    }
}

public sealed record CuentaAsiento(string Id, string Descripcion);

public sealed record VistaAsientos(
    ContextoCierre Contexto,
    string Moneda,
    IReadOnlyList<Sociedad> Sociedades,
    IReadOnlyList<CuentaAsiento> Cuentas,
    IReadOnlyList<AsientoBpc> Asientos,
    IReadOnlySet<string> Bloqueadas,
    string? Error);

public sealed record ResultadoAsiento(string? Numero, AsientoValidado Validado, string? Error);

/// <summary>
/// HU-39 (UX-14): asientos de ajuste de consolidación por grupo, periodo y moneda, que se contabilizan en BPC solo si
/// cuadran (RN-15) y sin líneas de sociedades bloqueadas (RN-17), con número, usuario y fecha (RNF-Aud-02).
/// </summary>
public sealed class ServicioAsientos(
    ContextoCierreLector contexto,
    AccesoSap acceso,
    ISapGateway sap,
    IRegistroEjecuciones registro,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string PasoGuardar = "Guardar asiento de ajuste";

    public async Task<VistaAsientos?> ObtenerAsync(Guid clienteId, string? periodo, string? moneda, CancellationToken ct = default)
    {
        var c = await contexto.LeerAsync(clienteId, periodo, ct);
        if (c is null)
        {
            return null;
        }

        var monedaElegida = moneda is not null && c.Monedas.Contains(moneda) ? moneda : c.Monedas.FirstOrDefault() ?? "";
        var (datos, error) = await LecturaBpc.LeerAsync(acceso, sap, clienteId, new ConsultaBpc(MotorConsolidacion.Modelo, c.PeriodoBpc, Origen: LecturaBpc.SinRegistros), ct);
        var asignaciones = await contexto.AsignacionesAsync(clienteId, c.Sociedades, ct);
        var cuentas = asignaciones.Select(a => new CuentaAsiento(GeneradorModeloBpc.IdCuenta(a.PlanCuentas, a.Cuenta), $"Cuenta {a.Cuenta} del plan {a.PlanCuentas}"))
            .Concat(ReglasRubros.CuentasTecnicas.Select(t => new CuentaAsiento(t.Id, t.Descripcion)))
            .DistinctBy(x => x.Id)
            .ToList();
        var asientos = (datos?.Asientos ?? []).Where(a => a.Grupo == c.Grupo).OrderByDescending(a => a.Numero, StringComparer.Ordinal).ToList();
        return new VistaAsientos(c, monedaElegida, ReglasCierre.Participantes(c.Sociedades), cuentas, asientos, ReglasCierreMensual.Bloqueadas(datos?.Bloqueos ?? []), error);
    }

    /// <summary>Valida el asiento con las sociedades, cuentas y bloqueos del periodo (sin guardar), para los totales de UX-14.</summary>
    public static AsientoValidado Validar(VistaAsientos v, IReadOnlyList<LineaAsientoTexto> lineas) =>
        ReglasAsientos.Validar(lineas, v.Sociedades.Select(s => s.Codigo).ToHashSet(), v.Cuentas.Select(x => x.Id).ToHashSet(), v.Bloqueadas, v.Contexto.Periodo);

    public async Task<ResultadoAsiento> GuardarAsync(Guid clienteId, string periodo, string moneda, string? descripcion, IReadOnlyList<LineaAsientoTexto> lineas, CancellationToken ct = default)
    {
        var v = await ObtenerAsync(clienteId, periodo, moneda, ct) ?? throw new KeyNotFoundException($"No existe el cliente {clienteId}.");
        var validado = Validar(v, lineas);
        if (v.Error is not null)
        {
            return new ResultadoAsiento(null, validado, v.Error);
        }

        if (!validado.Guardable)
        {
            return new ResultadoAsiento(null, validado, null);
        }

        var destino = await acceso.DestinoAsync(clienteId, SistemaSap.Bpc, ct);
        ResultadoOperacionSap r;
        if (destino.Destino is null)
        {
            r = new ResultadoOperacionSap(false, destino.Error!);
        }
        else
        {
            var definicion = Definicion.Json(new
            {
                logica = "ASIENTO",
                modelo = MotorConsolidacion.Modelo,
                grupo = v.Contexto.Grupo,
                periodo = v.Contexto.Periodo,
                moneda = v.Moneda,
                descripcion = (descripcion ?? "").Trim(),
                usuario = usuario.Nombre,
                fecha = reloj.GetUtcNow(),
                lineas = validado.Lineas.Select(l => new { sociedad = l.Sociedad, cuenta = l.Cuenta, intco = l.Intco, debito = l.Debito, credito = l.Credito, observacion = l.Observacion }),
            });
            try
            {
                r = await sap.EjecutarAsync(destino.Destino, new OperacionSap(FuncionesBpc.GuardarAsiento, "ASIENTO", $"Asiento {v.Contexto.Periodo}", definicion), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                r = new ResultadoOperacionSap(false, $"Error inesperado al llamar a SAP: {ex.Message}");
            }
        }

        var mensaje = SanitizadorMensajes.Limpiar(r.Mensaje, destino.Credencial);
        await registro.RegistrarAsync(new Ejecucion(reloj.GetUtcNow(), usuario.Nombre, clienteId, Textos.Sistema(SistemaSap.Bpc), PasoGuardar,
            $"{r.Clave ?? "Asiento"} {v.Contexto.Grupo} {v.Contexto.Periodo} {v.Moneda}", r.Exitosa ? ResultadoEjecucion.Exitoso : ResultadoEjecucion.Fallido, mensaje), ct);
        return r.Exitosa ? new ResultadoAsiento(r.Clave, validado, null) : new ResultadoAsiento(null, validado, mensaje);
    }
}
