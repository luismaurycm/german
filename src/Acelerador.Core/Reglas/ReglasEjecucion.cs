using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>Reglas de HU-14: qué pantalla corrige un error y desde dónde se reintenta.</summary>
public static class ReglasEjecucion
{
    public const string MensajeInterrumpida = "La ejecución se interrumpió al cerrar el acelerador. Reintenta desde este paso.";
    public const string MensajeEnCurso = "Ya hay una ejecución en curso para este cliente.";

    private static readonly string[] DeConexion =
        ["permiso", "autoriz", "not reached", "password", "credencial", "logon", "conexión a", "z_acc_paso", "comunicación"];

    /// <summary>
    /// HU-14 CA2: los errores de permiso, credencial o comunicación se corrigen en Conexiones (UX-02, donde está el usuario
    /// técnico); los demás, en el plan de generación (UX-08).
    /// </summary>
    public static string PantallaQueCorrige(string mensaje) =>
        DeConexion.Any(t => mensaje.Contains(t, StringComparison.OrdinalIgnoreCase)) ? Pasos.Conexiones : Pasos.Plan;

    /// <summary>
    /// HU-14 CA3: para reintentar, el paso con error vuelve a pendiente; los completos no se tocan. Devuelve false si no
    /// hay un paso con error.
    /// </summary>
    public static bool PrepararReintento(EjecucionPlan ejecucion)
    {
        var paso = ejecucion.PasoConError;
        if (paso is null)
        {
            return false;
        }

        paso.Estado = EstadoPaso.Pendiente;
        paso.Mensaje = null;
        paso.PantallaCorrige = null;
        paso.Inicio = null;
        paso.Fin = null;
        ejecucion.Fin = null;
        return true;
    }
}
