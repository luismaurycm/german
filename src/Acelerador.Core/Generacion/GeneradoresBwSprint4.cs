using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>
/// HU-16: bodega financiera de la variante BW/4HANA (RNF-Comp-05). Los InfoProviders son aDSO y no se genera ningún
/// InfoCubo; el extractor se conecta por ODP. Si la bodega existe, todo queda como Existente (RN-03).
/// </summary>
public sealed class GeneradorBodegaBw4Hana : IGeneradorObjetos
{
    public const string AdsoPartidas = "ZACC_D01";
    public const string AdsoSaldos = "ZACC_D02";
    public const string Grupo = GeneradorBodegaBw75.Grupo;

    public bool Aplica(ContextoPlan c) => c.Variante.InfoProvider == TipoInfoProvider.Adso && ReglasBw.TieneBodega(c.Cliente);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var bodega = c.Inventario.BodegaExistente;
        var extractor = ReglasBw.ExtractorFinanciero(c.Inventario);
        var campos = GeneradorBodegaBw75.CamposZIncorporables(c).ToList();
        EstadoObjeto Estado(string nombre) => bodega || ReglasBw.ExisteInfoProvider(c.Inventario, nombre) ? EstadoObjeto.Existente : EstadoObjeto.Nuevo;
        var nuevo = bodega ? EstadoObjeto.Existente : EstadoObjeto.Nuevo;
        const string paso = PasosGeneracion.ObjetosBw;

        yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Erp, "DataSource", extractor, "Partidas de contabilidad general (habilitar para ODP en el ERP)",
            ReglasBw.ExtractorActivo(c.Inventario, extractor) ? EstadoObjeto.Existente : EstadoObjeto.Nuevo, FuncionesZ.Activar,
            new { tipo = "DATASOURCE", nombre = extractor, odp = true });
        yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Bw, "DataSource", extractor, "DataSource ODP en BW/4HANA (contexto ODP_SAP)",
            nuevo, FuncionesZ.CrearObjeto, new { tipo = "DATASOURCE_ODP", nombre = extractor, sistemaFuente = "ODP_SAP" },
            "Conectado por ODP (HU-16 CA3).");

        var definicionCampos = new { caracteristicas = CamposBodega.Caracteristicas.Concat(campos), ratios = CamposBodega.Ratios };
        yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Bw, "aDSO", AdsoPartidas, "Partidas de contabilidad general (aDSO estándar)",
            Estado(AdsoPartidas), FuncionesZ.CrearObjeto, new { tipo = "ADSO", nombre = AdsoPartidas, modelo = "ESTANDAR", campos = definicionCampos, activar = true });
        yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Bw, "aDSO", AdsoSaldos, "Saldos de contabilidad general (aDSO para reporte)",
            Estado(AdsoSaldos), FuncionesZ.CrearObjeto, new { tipo = "ADSO", nombre = AdsoSaldos, modelo = "CUBO", campos = definicionCampos, activar = true });

        foreach (var (origen, destino) in new[] { (extractor, AdsoPartidas), (AdsoPartidas, AdsoSaldos) })
        {
            yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Bw, "Transformación", $"{origen}→{destino}", $"Transformación de {origen} a {destino}",
                nuevo, FuncionesZ.CrearObjeto, new { tipo = "TRFN", origen, destino, camposZ = campos, activar = true });
            yield return ReglasBw.Objeto(paso, Grupo, SistemaSap.Bw, "DTP", $"{origen}→{destino}", $"Proceso de transferencia de datos de {origen} a {destino}",
                nuevo, FuncionesZ.CrearObjeto, new { tipo = "DTP", origen, destino, activar = true });
        }
    }
}

/// <summary>
/// HU-18: cadenas de proceso de la bodega (RF-10, RN-04). Una cadena de carga full inicial que se ejecuta una vez como
/// parte del paso, y una cadena delta programada cada 24 horas (RNF-Rend-01). Ninguna borra peticiones solapadas: el
/// DSO o aDSO de partidas sobrescribe por documento y los InfoProviders de reporte cargan su delta desde él (CA3).
/// </summary>
public sealed class GeneradorCadenas : IGeneradorObjetos
{
    public const string CadenaFull = "ZACC_PC_FULL";
    public const string CadenaDelta = "ZACC_PC_DELTA";
    public const string Grupo = "Cadenas de proceso";
    public const int HorasDelta = 24;
    public const string HoraDelta = "02:00";

    public bool Aplica(ContextoPlan c) => ReglasBw.TieneBodega(c.Cliente);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c) => Cadenas(c, PasosGeneracion.ObjetosBw, Grupo, CadenaFull, CadenaDelta, Dtps(c), "la bodega financiera");

    /// <summary>DTP de la variante, en el orden en que se cargan.</summary>
    private static IReadOnlyList<string> Dtps(ContextoPlan c)
    {
        var extractor = ReglasBw.ExtractorFinanciero(c.Inventario);
        return c.Variante.InfoProvider == TipoInfoProvider.Adso
            ? [$"{extractor}→{GeneradorBodegaBw4Hana.AdsoPartidas}", $"{GeneradorBodegaBw4Hana.AdsoPartidas}→{GeneradorBodegaBw4Hana.AdsoSaldos}"]
            : [$"{extractor}→{GeneradorBodegaBw75.Dso}", $"{GeneradorBodegaBw75.Dso}→{GeneradorBodegaBw75.CuboSaldos}", $"{GeneradorBodegaBw75.Dso}→{GeneradorBodegaBw75.CuboMovimientos}"];
    }

    /// <summary>
    /// Cadena full + cadena delta nocturna + ejecución de la full (HU-18 CA4: la carga full se ve en UX-09). Las cadenas
    /// existentes no se recrean (RN-03); si la bodega ya existía, la carga full no se repite.
    /// </summary>
    public static IEnumerable<ObjetoPlan> Cadenas(ContextoPlan c, string paso, string grupo, string full, string delta, IReadOnlyList<string> dtps, string que)
    {
        var existe = c.Inventario.BodegaExistente && paso == PasosGeneracion.ObjetosBw;
        var estado = existe ? EstadoObjeto.Existente : EstadoObjeto.Nuevo;
        yield return ReglasBw.Objeto(paso, grupo, SistemaSap.Bw, "Cadena de proceso", full, $"Carga full inicial del histórico de {que}", estado, FuncionesZ.CrearObjeto,
            new { tipo = "CADENA", nombre = full, modo = "FULL", pasos = dtps, programacion = "UNA_VEZ", borrarPeticionesSolapadas = false });
        yield return ReglasBw.Objeto(paso, grupo, SistemaSap.Bw, "Cadena de proceso", delta, $"Carga delta de {que} cada {HorasDelta} horas ({HoraDelta})", estado, FuncionesZ.CrearObjeto,
            new { tipo = "CADENA", nombre = delta, modo = "DELTA", pasos = dtps, programacion = new { periodoHoras = HorasDelta, hora = HoraDelta }, borrarPeticionesSolapadas = false },
            "Después de la carga full inicial, las siguientes cargas son delta cada noche. RN-04");
        yield return ReglasBw.Objeto(paso, grupo, SistemaSap.Bw, "Ejecución", full, $"Ejecutar la carga full inicial de {que}", estado, FuncionesZ.EjecutarCadena,
            new { tipo = "EJECUCION", cadena = full, esperar = true });
    }
}

/// <summary>Un módulo que se queda en BW (RN-12): extractores, InfoProviders, cargas y cadenas, sin modelo en BPC.</summary>
public abstract class GeneradorModuloBw(Modulo modulo, string paso, string grupo, string prefijo, IReadOnlyList<(string Nombre, string Descripcion)> extractores, string descripcionDatos)
    : IGeneradorObjetos
{
    public string Prefijo => prefijo;

    public bool Aplica(ContextoPlan c) => c.Cliente.Modulos.Contains(modulo);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var adso = c.Variante.InfoProvider == TipoInfoProvider.Adso;
        var tipo = ReglasBw.TipoInfoProvider(c.Variante);
        var provider = $"ZACC_{prefijo}_{(adso ? "D" : "C")}01";
        var dtps = new List<string>();
        foreach (var (extractor, descripcion) in extractores)
        {
            yield return ReglasBw.Objeto(paso, grupo, SistemaSap.Erp, "DataSource", extractor, $"{descripcion} (activar en el ERP)",
                ReglasBw.ExtractorActivo(c.Inventario, extractor) ? EstadoObjeto.Existente : EstadoObjeto.Nuevo, FuncionesZ.Activar,
                new { tipo = "DATASOURCE", nombre = extractor, odp = adso });
            yield return ReglasBw.Objeto(paso, grupo, SistemaSap.Bw, "DataSource", extractor, adso ? $"DataSource ODP de {descripcion.ToLowerInvariant()}" : $"Replicación de {descripcion.ToLowerInvariant()}",
                EstadoObjeto.Nuevo, FuncionesZ.CrearObjeto, new { tipo = adso ? "DATASOURCE_ODP" : "DATASOURCE_REPLICA", nombre = extractor });
            dtps.Add($"{extractor}→{provider}");
        }

        yield return ReglasBw.Objeto(paso, grupo, SistemaSap.Bw, tipo, provider, descripcionDatos,
            ReglasBw.ExisteInfoProvider(c.Inventario, provider) ? EstadoObjeto.Existente : EstadoObjeto.Nuevo, FuncionesZ.CrearObjeto,
            new { tipo = adso ? "ADSO" : "INFOCUBO", nombre = provider, modulo = modulo.ToString(), activar = true }, "Se queda en BW; no crea modelo en BPC. RN-12");

        foreach (var dtp in dtps)
        {
            var origen = dtp.Split('→')[0];
            yield return ReglasBw.Objeto(paso, grupo, SistemaSap.Bw, "Transformación", dtp, $"Transformación de {origen} a {provider}", EstadoObjeto.Nuevo, FuncionesZ.CrearObjeto,
                new { tipo = "TRFN", origen, destino = provider, activar = true });
            yield return ReglasBw.Objeto(paso, grupo, SistemaSap.Bw, "DTP", dtp, $"Proceso de transferencia de datos de {origen} a {provider}", EstadoObjeto.Nuevo, FuncionesZ.CrearObjeto,
                new { tipo = "DTP", origen, destino = provider, activar = true });
        }

        foreach (var o in GeneradorCadenas.Cadenas(c, paso, grupo, $"ZACC_PC_{prefijo}_FULL", $"ZACC_PC_{prefijo}_DELTA", dtps, descripcionDatos.ToLowerInvariant()))
        {
            yield return o;
        }
    }
}

/// <summary>HU-19: módulo de activos fijos en BW (RF-24, RN-12).</summary>
public sealed class GeneradorActivosFijos() : GeneradorModuloBw(
    Modulo.ActivosFijos, PasosGeneracion.ActivosFijos, "Activos fijos", "AF",
    [("0FI_AA_11", "Movimientos de activos fijos"), ("0ASSET_ATTR", "Datos maestros de activos fijos")],
    "Movimientos y saldos de activos fijos");

/// <summary>
/// HU-20: módulo de deterioros en BW (RF-25, RN-12). Extractor estándar de partidas de clientes para el deterioro de
/// cartera (0FI_AR_4) y movimientos de activos para el deterioro de activos; se confirma en la prueba técnica HU-01.
/// </summary>
public sealed class GeneradorDeterioros() : GeneradorModuloBw(
    Modulo.Deterioros, PasosGeneracion.Deterioros, "Deterioros", "DE",
    [("0FI_AR_4", "Partidas de clientes para el deterioro de cartera")],
    "Movimientos del deterioro");
