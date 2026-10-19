using Acelerador.Core.Consolidacion;
using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>Estado de un paso del cierre de una sociedad en C04 (UX-15).</summary>
public enum EstadoCierre
{
    Pendiente,
    Completo,
    ConError,
    NoAplica,
}

/// <summary>Una celda de C04: el estado y, si hay error, la hoja que explica la causa (C01, C03 o la ejecución).</summary>
public sealed record CeldaCierre(EstadoCierre Estado, string? Causa = null);

/// <summary>Lo que se sabe de una sociedad en el periodo para armar su fila de C04.</summary>
public sealed record HechosCierre(
    MetodoConsolidacion Metodo,
    ValidacionCuadre? Validacion,
    bool SuperaUmbralIc,
    bool Convertida,
    bool Consolidado,
    string? PasoConError,
    bool ReportesGenerados);

/// <summary>HU-40 y HU-41: estados de trabajo (RN-17) y la matriz del estado del cierre (C04).</summary>
public static class ReglasCierreMensual
{
    public const string HojaC01 = "C01";
    public const string HojaC03 = "C03";
    public const string Ejecucion = "ejecucion";

    /// <summary>Pasos de C04, en el orden del flujo de cierre (HU-41 CA1).</summary>
    public static readonly IReadOnlyList<string> Pasos = ["Carga", "Cuadre", "Conciliación IC", "Conversión", "Consolidación", "Reportes"];

    public static IReadOnlySet<string> Bloqueadas(IEnumerable<BloqueoBpc> bloqueos) =>
        bloqueos.Where(b => b.Bloqueada).Select(b => b.Sociedad).ToHashSet();

    /// <summary>
    /// La fila de una sociedad: la carga y el cuadre salen de la validación de cuadre (C03); la conciliación de C01; la
    /// conversión y la consolidación de los datos de BPC o del paso que falló; los reportes del catálogo (UX-10).
    /// </summary>
    public static IReadOnlyList<CeldaCierre> Fila(HechosCierre h)
    {
        var importada = h.Validacion is { Importada: true };
        var fuera = h.Metodo == MetodoConsolidacion.FueraDelPerimetro;
        CeldaCierre Pendiente() => new(EstadoCierre.Pendiente);
        CeldaCierre Error(string causa) => new(EstadoCierre.ConError, causa);
        CeldaCierre Completo() => new(EstadoCierre.Completo);

        var carga = h.Validacion is null ? Pendiente() : Completo();
        var cuadre = h.Validacion is null ? Pendiente() : h.Validacion.Importable ? Completo() : Error(HojaC03);
        var conciliacion = fuera ? new CeldaCierre(EstadoCierre.NoAplica) : !importada ? Pendiente() : h.SuperaUmbralIc ? Error(HojaC01) : Completo();
        var conversion = !importada ? Pendiente() : h.Convertida ? Completo() : h.PasoConError == PasosGeneracion.Conversion ? Error(Ejecucion) : Pendiente();
        var consolidacion = fuera ? new CeldaCierre(EstadoCierre.NoAplica)
            : !importada || !h.Convertida ? Pendiente()
            : h.Consolidado ? Completo()
            : h.PasoConError is PasosGeneracion.Consolidacion or PasosGeneracion.Inversion ? Error(Ejecucion) : Pendiente();
        var reportes = consolidacion.Estado is EstadoCierre.Completo or EstadoCierre.NoAplica && h.ReportesGenerados ? Completo() : Pendiente();
        return [carga, cuadre, conciliacion, conversion, consolidacion, reportes];
    }

    public static string Texto(EstadoCierre e) => e switch
    {
        EstadoCierre.Completo => "Completo",
        EstadoCierre.ConError => "Con error",
        EstadoCierre.NoAplica => "No aplica",
        _ => "Pendiente",
    };

    public static string NoIniciado(string periodo) => $"El cierre de {periodo} aún no ha iniciado.";

    /// <summary>Aviso de UX-15 cuando hay sociedades que no cuadran: «2 sociedades no cuadran. 3000 y 9000 no se importaron…».</summary>
    public static string? AvisoCuadre(IReadOnlyList<string> sinCuadre) => sinCuadre.Count switch
    {
        0 => null,
        1 => $"1 sociedad no cuadra. {sinCuadre[0]} no se importó; revisa C03 Validación de cuadre antes de consolidar.",
        _ => $"{sinCuadre.Count} sociedades no cuadran. {Textos.Lista(sinCuadre)} no se importaron; revisa C03 Validación de cuadre antes de consolidar.",
    };

    /// <summary>RN-17 en las cargas: «La sociedad 9000 está bloqueada en 2026-09: no admite cargas hasta que se desbloquee».</summary>
    public static string MensajeBloqueada(string sociedad, string periodo, string que) =>
        $"La sociedad {sociedad} está bloqueada en {periodo.Replace('.', '-')}: {que} hasta que se desbloquee (RN-17).";
}
