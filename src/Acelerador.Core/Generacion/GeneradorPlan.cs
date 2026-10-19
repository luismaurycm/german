using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;

namespace Acelerador.Core.Generacion;

/// <summary>Arma el plan de generación (HU-13) con los generadores de cada área y los pasos que faltan.</summary>
public sealed class GeneradorPlan(IReadOnlyList<IGeneradorObjetos> generadores, CatalogoVariantes catalogo)
{
    /// <summary>Generadores de la versión 1, en el orden en que contribuyen a los pasos.</summary>
    public static IReadOnlyList<IGeneradorObjetos> VersionUno { get; } =
    [
        new GeneradorBodegaBw75(),
        new GeneradorBodegaBw4Hana(),
        new GeneradorCamposZ(),
        new GeneradorCadenas(),
        new GeneradorActivosFijos(),
        new GeneradorDeterioros(),
        new GeneradorNoSap(),
        new GeneradorDatosMaestros(),
        new GeneradorModeloBpc(),
        new GeneradorPropiedad(),
        new GeneradorScripts(),
        new GeneradorConsolidacion(),
    ];

    public static GeneradorPlan Predeterminado { get; } = new(VersionUno, CatalogoVariantes.VersionUno);

    public PlanGeneracion Armar(
        Cliente cliente,
        Inventario? inventario,
        IReadOnlyList<Sociedad> sociedades,
        IReadOnlyList<AsignacionCuenta> asignaciones,
        IReadOnlyCollection<string>? noSapCargadas = null,
        DateTimeOffset? fecha = null)
    {
        var faltantes = Faltantes(cliente, inventario, sociedades, asignaciones.Count, noSapCargadas);
        var variante = catalogo.Buscar(cliente.Deteccion?.VarianteId);
        var monedas = new MonedasPlan(
            sociedades.Select(s => (s.Codigo, s.MonedaLocal)).ToList(),
            ReglasMonedas.TiposDeTasa);
        if (faltantes.Count > 0 || variante is null || inventario is null)
        {
            return new PlanGeneracion(variante, [], monedas, faltantes);
        }

        var contexto = new ContextoPlan(cliente, variante, inventario, sociedades, asignaciones, fecha ?? DateTimeOffset.UtcNow);
        var objetos = generadores.Where(g => g.Aplica(contexto)).SelectMany(g => g.Generar(contexto)).ToList();
        var pasos = objetos
            .GroupBy(o => o.PasoId)
            .OrderBy(g => PasosGeneracion.Orden(g.Key))
            .Select(g => new PasoPlan(g.Key, PasosGeneracion.Nombre(g.Key), g.ToList()))
            .ToList();
        return new PlanGeneracion(variante, pasos, monedas, faltantes);
    }

    /// <summary>HU-13 CA4: qué paso anterior falta, en el orden de la navegación, con la pantalla que lo completa.</summary>
    public static IReadOnlyList<Faltante> Faltantes(
        Cliente cliente, Inventario? inventario, IReadOnlyList<Sociedad> sociedades, int cuentasMapeadas, IReadOnlyCollection<string>? noSapCargadas = null)
    {
        var faltan = new List<Faltante>();
        var bloqueo = ReglaAvance.BloqueoDeConfiguracion(cliente);
        if (bloqueo is not null || cliente.Deteccion is null)
        {
            faltan.Add(new Faltante(Pasos.Conexiones, bloqueo ?? "Identifica la arquitectura y la versión en Conexiones."));
            return faltan;
        }

        var inv = ReglasInventario.BloqueoParaContinuar(inventario);
        if (inv is not null)
        {
            faltan.Add(new Faltante(Pasos.Diagnostico, $"Diagnóstico: {inv}"));
        }

        if (cliente.Modulos.Count == 0)
        {
            faltan.Add(new Faltante(Pasos.Modulos, $"Módulos: {ReglasModulos.MensajeSinModulos}"));
        }

        var soc = ReglasSociedades.BloqueoParaContinuar(sociedades);
        if (soc is not null)
        {
            faltan.Add(new Faltante(Pasos.Sociedades, $"Sociedades: {soc}"));
        }

        if (sociedades.Any(s => s.EnSap) && cuentasMapeadas == 0)
        {
            faltan.Add(new Faltante(Pasos.Mapeo, "Mapeo de cuentas: aún no hay cuentas mapeadas a los rubros."));
        }

        var sinCarga = sociedades.Where(s => !s.EnSap && !(noSapCargadas ?? []).Contains(s.Codigo)).Select(s => s.Codigo).ToList();
        if (sinCarga.Count > 0)
        {
            faltan.Add(new Faltante(Pasos.NoSap, $"Sociedades no SAP: falta cargar el archivo de {string.Join(", ", sinCarga)}."));
        }

        return faltan;
    }
}
