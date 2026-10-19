using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Core.Generacion;

/// <summary>Funciones del paquete Z que usan los generadores (abap/README.md).</summary>
public static class FuncionesZ
{
    public const string CrearObjeto = "Z_ACC_CREAR_OBJETO";
    public const string Activar = "Z_ACC_ACTIVAR";
    public const string BpcCrearMiembro = "Z_ACC_BPC_CREAR_MIEMBRO";
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

    public bool Aplica(ContextoPlan c) =>
        c.Variante.InfoProvider == TipoInfoProvider.InfoCubo
        && (c.Cliente.Modulos.Contains(Modulo.Consolidacion) || c.Cliente.Modulos.Contains(Modulo.Presupuesto));

    public IEnumerable<ObjetoPlan> Generar(ContextoPlan c)
    {
        var bodega = c.Inventario.BodegaExistente;
        var campos = CamposZIncorporables(c).ToList();
        var extractorActivo = c.Inventario.Lista(TipoInventario.Extractores).Any(e => e.NombreTecnico == Extractor && e.Activo);

        yield return Objeto(SistemaSap.Erp, "DataSource", Extractor, "Partidas individuales de contabilidad general (activar en el ERP)",
            extractorActivo ? EstadoObjeto.Existente : EstadoObjeto.Nuevo, FuncionesZ.Activar, new { tipo = "DATASOURCE", nombre = Extractor });

        EstadoObjeto Estado(string nombre) =>
            bodega || c.Inventario.Lista(TipoInventario.InfoProviders).Any(o => o.NombreTecnico == nombre) ? EstadoObjeto.Existente : EstadoObjeto.Nuevo;

        yield return Objeto(SistemaSap.Bw, "DataSource", Extractor, "Replicación del DataSource desde el sistema fuente ERP",
            bodega ? EstadoObjeto.Existente : EstadoObjeto.Nuevo, FuncionesZ.CrearObjeto, new { tipo = "DATASOURCE_REPLICA", nombre = Extractor });

        var definicionCampos = new { caracteristicas = CamposBodega.Caracteristicas.Concat(campos), ratios = CamposBodega.Ratios };
        yield return Objeto(SistemaSap.Bw, "DSO", Dso, "Partidas de contabilidad general (DSO estándar)", Estado(Dso), FuncionesZ.CrearObjeto,
            new { tipo = "DSO", nombre = Dso, campos = definicionCampos, activar = true });
        yield return Objeto(SistemaSap.Bw, "InfoCubo", CuboSaldos, "Saldos de contabilidad general", Estado(CuboSaldos), FuncionesZ.CrearObjeto,
            new { tipo = "INFOCUBO", nombre = CuboSaldos, campos = definicionCampos, activar = true });
        yield return Objeto(SistemaSap.Bw, "InfoCubo", CuboMovimientos, "Movimientos de contabilidad general por periodo", Estado(CuboMovimientos), FuncionesZ.CrearObjeto,
            new { tipo = "INFOCUBO", nombre = CuboMovimientos, campos = definicionCampos, activar = true });

        foreach (var (origen, destino) in new[] { (Extractor, Dso), (Dso, CuboSaldos), (Dso, CuboMovimientos) })
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

    public bool Aplica(ContextoPlan c) => c.Inventario.CamposZIncorporados.Count > 0
        && (c.Cliente.Modulos.Contains(Modulo.Consolidacion) || c.Cliente.Modulos.Contains(Modulo.Presupuesto));

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
