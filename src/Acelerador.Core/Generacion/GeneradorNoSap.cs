using Acelerador.Core.Servicios;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>
/// Paso «Sociedades no SAP» (RF-15, INT-04): fuente de datos sobre la tabla de staging ZACC_NOSAP (la llena UX-07), su
/// transformación y DTP hacia la bodega, y la carga de lo confirmado. Así los datos no SAP llegan al mismo modelo.
/// </summary>
public sealed class GeneradorNoSap : IGeneradorObjetos
{
    public const string Grupo = "Sociedades no SAP";
    public const string DataSource = "ZACC_NOSAP_DS";

    public bool Aplica(ContextoPlan c) => c.Sociedades.Any(s => !s.EnSap) && ReglasBw.TieneBodega(c.Cliente);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        const string paso = PasosGeneracion.NoSap;
        var destino = c.Variante.InfoProvider == Reglas.TipoInfoProvider.Adso ? GeneradorBodegaBw4Hana.AdsoPartidas : GeneradorBodegaBw75.Dso;
        var noSap = c.Sociedades.Where(s => !s.EnSap).Select(s => s.Codigo).ToList();
        yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Bw, "DataSource", DataSource, $"Fuente de datos genérica sobre la tabla {ServicioNoSap.TablaStaging}",
            EstadoObjeto.Nuevo, FuncionesZ.CrearObjeto, new { tipo = "DATASOURCE_TABLA", nombre = DataSource, tabla = ServicioNoSap.TablaStaging });
        yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Bw, "Transformación", $"{DataSource}→{destino}", $"Transformación de {DataSource} a {destino}",
            EstadoObjeto.Nuevo, FuncionesZ.CrearObjeto, new { tipo = "TRFN", origen = DataSource, destino, activar = true });
        yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Bw, "DTP", $"{DataSource}→{destino}", $"Proceso de transferencia de datos de {DataSource} a {destino}",
            EstadoObjeto.Nuevo, FuncionesZ.CrearObjeto, new { tipo = "DTP", origen = DataSource, destino, activar = true });
        yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Bw, "Ejecución", $"{DataSource}→{destino}", $"Cargar a la bodega lo confirmado de {string.Join(", ", noSap)}",
            EstadoObjeto.Nuevo, FuncionesZ.EjecutarCadena, new { tipo = "EJECUCION", dtp = $"{DataSource}→{destino}", sociedades = noSap },
            "Antes de llegar a BPC, cada sociedad pasa por la validación de cuadre (HU-36).");
    }
}
