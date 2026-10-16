using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>RN-01: la validación de conexión ERP–BW aplica cuando ERP y BW están en sistemas separados (HU-05 CA1).</summary>
public static class ReglaRn01
{
    /// <summary>
    /// Si las dos conexiones ya respondieron, decide por el SID. Si no, por host y mandante:
    /// el mismo host y mandante es el mismo sistema. Sin datos se asume separados (arquitectura A,
    /// la única soportada en la versión 1), y el botón aparece deshabilitado hasta probar ambas.
    /// </summary>
    public static bool AplicaValidacionErpBw(Conexion erp, Conexion bw)
    {
        if (!string.IsNullOrEmpty(erp.Sid) && !string.IsNullOrEmpty(bw.Sid))
        {
            return !string.Equals(erp.Sid, bw.Sid, StringComparison.OrdinalIgnoreCase);
        }

        if (erp.Host.Length == 0 || bw.Host.Length == 0)
        {
            return true;
        }

        var mismoSistema = string.Equals(erp.Host, bw.Host, StringComparison.OrdinalIgnoreCase) && erp.Mandante == bw.Mandante;
        return !mismoSistema;
    }

    /// <summary>La validación solo se puede lanzar con ERP y BW conectados.</summary>
    public static bool PuedeValidar(Conexion erp, Conexion bw) =>
        erp.Estado == EstadoPrueba.Exitosa && bw.Estado == EstadoPrueba.Exitosa;
}
