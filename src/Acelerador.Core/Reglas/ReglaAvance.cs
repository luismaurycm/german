using Acelerador.Core.Dominio;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>
/// Cuándo se habilita «Continuar al diagnóstico» en UX-02. Reúne HU-03 CA3 (conexiones exitosas),
/// HU-05 CA3 (validación ERP–BW cuando aplica), HU-06 CA3 (variante disponible) y HU-57 CA2 (arquitectura B).
/// Devuelve los motivos pendientes en el orden en que el consultor los resuelve; sin motivos, puede continuar.
/// </summary>
public static class ReglaAvance
{
    public static IReadOnlyList<string> PendientesParaDiagnostico(Cliente cliente, IReadOnlyList<Conexion> conexiones, CatalogoVariantes catalogo)
    {
        var pendientes = new List<string>();

        foreach (var sistema in Enum.GetValues<SistemaSap>())
        {
            var c = conexiones.FirstOrDefault(x => x.Sistema == sistema);
            if (c is null || c.Estado != EstadoPrueba.Exitosa)
            {
                pendientes.Add($"Prueba con éxito la conexión a {Textos.Sistema(sistema)}.");
            }
        }

        var erp = conexiones.FirstOrDefault(x => x.Sistema == SistemaSap.Erp);
        var bw = conexiones.FirstOrDefault(x => x.Sistema == SistemaSap.Bw);
        if (erp is not null && bw is not null && ReglaRn01.AplicaValidacionErpBw(erp, bw) && cliente.Validacion?.Exitosa != true)
        {
            pendientes.Add("Valida con éxito la conexión ERP–BW.");
        }

        if (cliente.Deteccion is null)
        {
            if (pendientes.Count == 0)
            {
                pendientes.Add("Identifica la arquitectura y la versión.");
            }
        }
        else
        {
            var clasificacion = ClasificadorArquitectura.Describir(cliente.Deteccion, catalogo);
            if (!clasificacion.Soportada)
            {
                pendientes.Add(clasificacion.Titulo + ".");
            }
        }

        return pendientes;
    }

    /// <summary>
    /// Un paso se abre si el cliente ya llegó a él. Fuera de Conexiones, además, el cliente debe estar
    /// soportado: a un cliente con arquitectura B o sin variante no se le configura nada (RN-19). UX-07 solo se abre
    /// si alguna sociedad no está en SAP (HU-09 CA5).
    /// </summary>
    public static bool PuedeAbrir(Cliente cliente, string paso) =>
        Pasos.Indice(paso) >= 0
        && Pasos.Indice(paso) <= Pasos.Indice(cliente.PasoActual)
        && (paso == Pasos.Conexiones || BloqueoDeConfiguracion(cliente) is null)
        && (paso != Pasos.NoSap || cliente.TieneSociedadesNoSap);

    /// <summary>RN-19: un cliente con arquitectura B o sin variante no se configura (UX-04 en adelante).</summary>
    public static string? BloqueoDeConfiguracion(Cliente cliente) =>
        cliente.Estado == EstadoCliente.NoSoportado ? cliente.MotivoEstado ?? "Cliente no soportado en la versión 1." : null;
}
