using Acelerador.Core.Dominio;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>Textos visibles de versiones, sistemas y estados, compartidos por las pantallas y el registro.</summary>
public static class Textos
{
    public static string Sistema(SistemaSap sistema) => sistema switch
    {
        SistemaSap.Erp => "SAP ERP",
        SistemaSap.Bw => "SAP BW",
        _ => "SAP BPC",
    };

    public static string Version(VersionBw bw) => bw.Producto switch
    {
        ProductoBw.Bw75 => "BW 7.5",
        ProductoBw.Bw4Hana => $"BW/4HANA {bw.Release}",
        _ => bw.Release.StartsWith("BW", StringComparison.OrdinalIgnoreCase) ? bw.Release : $"BW {bw.Release}",
    };

    public static string Version(VersionBpc bpc)
    {
        var modelo = bpc.Modelo == ModeloBpc.Estandar ? "modelo estándar" : "modelo embebido";
        return $"BPC {bpc.Release} {modelo}";
    }

    /// <summary>Texto corto de la arquitectura para UX-01 y la insignia de la cabecera.</summary>
    public static string Arquitectura(DeteccionArquitectura? d) => d switch
    {
        null => "Sin detectar",
        { Arquitectura: Dominio.Arquitectura.B } => "B · S/4HANA, BW y BPC embebidos",
        _ => $"A · {Version(d.Bw)} + {Version(d.Bpc)}",
    };

    public static string Estado(EstadoCliente estado) => estado switch
    {
        EstadoCliente.NoSoportado => "No soportado",
        EstadoCliente.ConError => "Con error",
        EstadoCliente.Completo => "Completo",
        _ => "En curso",
    };

    public static string Resultado(ResultadoEjecucion r) => r switch
    {
        ResultadoEjecucion.Exitoso => "Exitoso",
        ResultadoEjecucion.Fallido => "Fallido",
        _ => "No soportado",
    };

    public static string Estado(EstadoPaso estado) => estado switch
    {
        EstadoPaso.Completo => "Completo",
        EstadoPaso.Existente => "Existente",
        EstadoPaso.EnCurso => "En curso",
        EstadoPaso.Error => "Con error",
        _ => "Pendiente",
    };

    /// <summary>Duración como en el prototipo: «6:30» (minutos y segundos).</summary>
    public static string Duracion(TimeSpan? duracion) => duracion is null
        ? ""
        : $"{(int)duracion.Value.TotalMinutes}:{duracion.Value.Seconds:00}";

    /// <summary>«1 objeto», «3 objetos»: cantidad con el sustantivo en singular o plural.</summary>
    public static string Cantidad(int n, string singular, string plural) => $"{n} {(n == 1 ? singular : plural)}";

    /// <summary>«3000», «3000 y 9000», «1000, 3000 y 9000».</summary>
    public static string Lista(IReadOnlyList<string> elementos) =>
        elementos.Count <= 1 ? string.Join("", elementos) : $"{string.Join(", ", elementos.Take(elementos.Count - 1))} y {elementos[^1]}";
}
