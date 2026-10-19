using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>Funciones del paquete Z que usan los generadores (abap/README.md).</summary>
public static class FuncionesZ
{
    public const string CrearObjeto = "Z_ACC_CREAR_OBJETO";
    public const string Activar = "Z_ACC_ACTIVAR";
    public const string EjecutarCadena = "Z_ACC_EJECUTAR_CADENA";
    public const string CargarArchivo = "Z_ACC_CARGAR_ARCHIVO";
    public const string BpcCrearMiembro = "Z_ACC_BPC_CREAR_MIEMBRO";
}

/// <summary>Ayudas comunes de los generadores de BW.</summary>
public static class ReglasBw
{
    /// <summary>
    /// Extractor financiero del ERP (RF-08, INT-01): 0FI_GL_14 en ECC; en S/4HANA, 0FI_ACDOCA_10 sobre ACDOCA por ODP
    /// (por confirmar en la pregunta abierta 11 del DAF). Se elige por lo que el inventario encontró en el ERP.
    /// </summary>
    public static string ExtractorFinanciero(Inventario inventario) =>
        inventario.Lista(TipoInventario.Extractores).Any(e => e.NombreTecnico == "0FI_ACDOCA_10") ? "0FI_ACDOCA_10" : GeneradorBodegaBw75.Extractor;

    public static bool ExtractorActivo(Inventario inventario, string extractor) =>
        inventario.Lista(TipoInventario.Extractores).Any(e => e.NombreTecnico == extractor && e.Activo);

    public static bool ExisteInfoProvider(Inventario inventario, string nombre) =>
        inventario.Lista(TipoInventario.InfoProviders).Any(o => o.NombreTecnico == nombre);

    /// <summary>Tipo de InfoProvider de la variante (RNF-Comp-05: aDSO en BW/4HANA).</summary>
    public static string TipoInfoProvider(VarianteGeneracion v) => v.InfoProvider == Reglas.TipoInfoProvider.Adso ? "aDSO" : "InfoCubo";

    public static bool TieneBodega(Cliente c) => c.Modulos.Contains(Modulo.Consolidacion) || c.Modulos.Contains(Modulo.Presupuesto);

    public static ObjetoPlan Objeto(string paso, string grupo, SistemaSap sistema, string tipo, string nombre, string descripcion,
        EstadoObjeto estado, string funcion, object definicion, string? nota = null) =>
        new(paso, Capa.Bw, grupo, sistema, tipo, nombre, descripcion, estado,
            new OperacionSap(funcion, tipo.ToUpperInvariant(), nombre, Definicion.Json(definicion)), nota);
}

/// <summary>InfoObjects estándar de la bodega financiera (contenido de negocio de SAP).</summary>
public static class CamposBodega
{
    public static readonly IReadOnlyList<string> Caracteristicas =
        ["0COMP_CODE", "0CHRT_ACCTS", "0GL_ACCOUNT", "0FISCPER", "0FISCVARNT", "0COSTCENTER", "0PROFIT_CTR", "0PCOMPANY", "0CURTYPE", "0CURRENCY"];

    public static readonly IReadOnlyList<string> Ratios = ["0DEBIT", "0CREDIT", "0BALANCE"];
}

/// <summary>
/// HU-15: bodega financiera de la variante BW 7.5 (InfoCubos). Activa 0FI_GL_14 en el ERP, lo replica en BW y crea el
/// DSO de partidas, los InfoCubos de saldos y movimientos, y sus transformaciones y DTP. Si la bodega existe, todo
/// queda como Existente (RN-03).
/// </summary>
public sealed class GeneradorBodegaBw75 : IGeneradorObjetos
{
    public const string Extractor = "0FI_GL_14";
    public const string Dso = "ZACC_O01";
    public const string CuboSaldos = "ZACC_C01";
    public const string CuboMovimientos = "ZACC_C02";
    public const string Grupo = "Bodega financiera";

    public bool Aplica(ContextoPlan c) => c.Variante.InfoProvider == TipoInfoProvider.InfoCubo && ReglasBw.TieneBodega(c.Cliente);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var bodega = c.Inventario.BodegaExistente;
        var campos = CamposZIncorporables(c).ToList();
        var extractor = ReglasBw.ExtractorFinanciero(c.Inventario);

        yield return Objeto(SistemaSap.Erp, "DataSource", extractor, "Partidas individuales de contabilidad general (activar en el ERP)",
            ReglasBw.ExtractorActivo(c.Inventario, extractor) ? EstadoObjeto.Existente : EstadoObjeto.Nuevo, FuncionesZ.Activar, new { tipo = "DATASOURCE", nombre = extractor });

        EstadoObjeto Estado(string nombre) =>
            bodega || c.Inventario.Lista(TipoInventario.InfoProviders).Any(o => o.NombreTecnico == nombre) ? EstadoObjeto.Existente : EstadoObjeto.Nuevo;

        yield return Objeto(SistemaSap.Bw, "DataSource", extractor, "Replicación del DataSource desde el sistema fuente ERP",
            bodega ? EstadoObjeto.Existente : EstadoObjeto.Nuevo, FuncionesZ.CrearObjeto, new { tipo = "DATASOURCE_REPLICA", nombre = extractor, sistemaFuente = "SAP" });

        var definicionCampos = new { caracteristicas = CamposBodega.Caracteristicas.Concat(campos), ratios = CamposBodega.Ratios };
        yield return Objeto(SistemaSap.Bw, "DSO", Dso, "Partidas de contabilidad general (DSO estándar)", Estado(Dso), FuncionesZ.CrearObjeto,
            new { tipo = "DSO", nombre = Dso, campos = definicionCampos, activar = true });
        yield return Objeto(SistemaSap.Bw, "InfoCubo", CuboSaldos, "Saldos de contabilidad general", Estado(CuboSaldos), FuncionesZ.CrearObjeto,
            new { tipo = "INFOCUBO", nombre = CuboSaldos, campos = definicionCampos, activar = true });
        yield return Objeto(SistemaSap.Bw, "InfoCubo", CuboMovimientos, "Movimientos de contabilidad general por periodo", Estado(CuboMovimientos), FuncionesZ.CrearObjeto,
            new { tipo = "INFOCUBO", nombre = CuboMovimientos, campos = definicionCampos, activar = true });

        foreach (var (origen, destino) in new[] { (extractor, Dso), (Dso, CuboSaldos), (Dso, CuboMovimientos) })
        {
            var estado = bodega ? EstadoObjeto.Existente : EstadoObjeto.Nuevo;
            yield return Objeto(SistemaSap.Bw, "Transformación", $"{origen}→{destino}", $"Transformación de {origen} a {destino}", estado, FuncionesZ.CrearObjeto,
                new { tipo = "TRFN", origen, destino, camposZ = campos, activar = true });
            yield return Objeto(SistemaSap.Bw, "DTP", $"{origen}→{destino}", $"Proceso de transferencia de datos de {origen} a {destino}", estado, FuncionesZ.CrearObjeto,
                new { tipo = "DTP", origen, destino, activar = true });
        }
    }

    /// <summary>Campos Z elegidos y compatibles que entran en la definición de la bodega nueva (RF-09).</summary>
    public static IEnumerable<string> CamposZIncorporables(ContextoPlan c) =>
        c.Inventario.Lista(TipoInventario.CamposZ)
            .Where(z => c.Inventario.CamposZIncorporados.Contains(z.NombreTecnico) && ReglasCamposZ.Compatible(z.TipoDato))
            .Select(z => ReglasCamposZ.InfoObject(z.NombreTecnico));

    private static ObjetoPlan Objeto(SistemaSap sistema, string tipo, string nombre, string descripcion, EstadoObjeto estado, string funcion, object definicion) =>
        new(PasosGeneracion.ObjetosBw, Capa.Bw, Grupo, sistema, tipo, nombre, descripcion, estado,
            new OperacionSap(funcion, tipo.ToUpperInvariant(), nombre, Definicion.Json(definicion)));
}

/// <summary>
/// HU-17: cada campo Z elegido en UX-03 es un InfoObject nuevo en BW. Uno con tipo de dato incompatible queda «No
/// incorporable» con el motivo (CA3). Si la bodega ya existía, el InfoObject se crea pero la bodega no se modifica (RN-03).
/// </summary>
public sealed class GeneradorCamposZ : IGeneradorObjetos
{
    public const string Grupo = "Campos Z";

    public bool Aplica(ContextoPlan c) => c.Inventario.CamposZIncorporados.Count > 0 && ReglasBw.TieneBodega(c.Cliente);

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        foreach (var z in c.Inventario.Lista(TipoInventario.CamposZ).Where(z => c.Inventario.CamposZIncorporados.Contains(z.NombreTecnico)))
        {
            var motivo = ReglasCamposZ.MotivoIncompatible(z.TipoDato);
            var nombre = ReglasCamposZ.InfoObject(z.NombreTecnico);
            var nota = motivo ?? (c.Inventario.BodegaExistente
                ? "La bodega existente no se modifica: el InfoObject queda disponible para agregarlo cuando el cliente lo decida."
                : $"Se agrega a los InfoProviders de la bodega desde {z.Detalle}.");
            yield return new ObjetoPlan(
                PasosGeneracion.ObjetosBw, Capa.Bw, Grupo, SistemaSap.Bw, "InfoObject", nombre, $"{z.Descripcion} ({z.TipoDato})",
                motivo is null ? EstadoObjeto.Nuevo : EstadoObjeto.NoIncorporable,
                new OperacionSap(FuncionesZ.CrearObjeto, "IOBJ", nombre,
                    Definicion.Json(new { tipo = "IOBJ", nombre, tipoDato = z.TipoDato, tablaOrigen = z.Detalle, campoOrigen = z.NombreTecnico, activar = true })),
                nota);
        }
    }
}
