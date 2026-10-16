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
        _ => "En curso",
    };

    public static string Resultado(ResultadoEjecucion r) => r switch
    {
        ResultadoEjecucion.Exitoso => "Exitoso",
        ResultadoEjecucion.Fallido => "Fallido",
        _ => "No soportado",
    };
}
