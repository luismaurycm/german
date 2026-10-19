using System.Text.Json;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;

namespace Acelerador.Core.Consolidacion;

/// <summary>
/// La lógica que BPC ejecuta con lo que el acelerador despliega, para el simulador: validación de cuadre (RN-16), paquete de
/// importación (RF-17), conversión (RF-26, RF-33) y eliminación intercompañía (RF-19). Lee los parámetros de la definición
/// JSON que reciben las funciones Z y usa <see cref="MotorConsolidacion"/>.
/// </summary>
public sealed class LogicaBpcReferencia : ILogicaSimulada
{
    public ResultadoOperacionSap? Ejecutar(OperacionSap operacion, IAlmacenSimulado almacen, OpcionesSimulador opciones)
    {
        using var doc = JsonDocument.Parse(operacion.Definicion);
        var d = doc.RootElement;
        return d.GetProperty("logica").GetString() switch
        {
            "VALIDAR_CUADRE" => ValidarCuadre(d, almacen),
            "IMPORTAR" => Importar(d, almacen),
            "CONVERSION" => Convertir(d, almacen),
            "ELIMINACION_IC" => EliminarIntercompania(d, almacen),
            _ => null,
        };
    }

    private static IReadOnlyList<SaldoErp> Datos(JsonElement d, IAlmacenSimulado almacen)
    {
        var sociedad = d.GetProperty("sociedad").GetString()!;
        return d.GetProperty("enSap").GetBoolean() ? almacen.SaldosErp(sociedad) : almacen.DatosNoSap(sociedad, d.GetProperty("periodo").GetString()!);
    }

    private static ResultadoOperacionSap ValidarCuadre(JsonElement d, IAlmacenSimulado almacen)
    {
        var sociedad = d.GetProperty("sociedad").GetString()!;
        var periodo = d.GetProperty("periodo").GetString()!;
        var tolerancia = d.GetProperty("tolerancia").GetDecimal();
        var mapeo = d.GetProperty("mapeo").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        var v = ReglaCuadre.Validar(sociedad, periodo, Datos(d, almacen), mapeo, tolerancia);
        almacen.GuardarValidacion(v);
        return v.Importable
            ? new ResultadoOperacionSap(true, $"{sociedad} cuadra en {periodo} (diferencia de balance {ReglaCuadre.Formato(v.DiferenciaBalance)}).", ResultadoEscritura.Creado, Cantidad: 1)
            : new ResultadoOperacionSap(true, $"{sociedad} no cuadra en {periodo}.", ResultadoEscritura.Creado, Cantidad: 0, Aviso: ReglaCuadre.Motivo(v, tolerancia));
    }

    private static ResultadoOperacionSap Importar(JsonElement d, IAlmacenSimulado almacen)
    {
        var sociedad = d.GetProperty("sociedad").GetString()!;
        var periodo = d.GetProperty("periodo").GetString()!;
        var validacion = almacen.Validaciones.FirstOrDefault(v => v.Sociedad == sociedad && v.Periodo == periodo);
        if (validacion is not { Importable: true })
        {
            // HU-27 CA3: solo se importan las sociedades que pasan la validación de cuadre.
            return new ResultadoOperacionSap(true, $"{sociedad}: 0 registros importados.", ResultadoEscritura.Creado, Cantidad: 0,
                Aviso: $"{sociedad} no se importa en {periodo}: no pasó la validación de cuadre (ver C03).");
        }

        var grupo = d.GetProperty("grupo").EnumerateArray().Select(x => x.GetString()!).ToHashSet();
        var registros = MotorConsolidacion.Importar(sociedad, d.GetProperty("plan").GetString()!, periodo, Datos(d, almacen), grupo);
        var periodoBpc = MotorConsolidacion.PeriodoBpc(periodo);
        almacen.Reemplazar(r => r.Entidad == sociedad && r.Periodo == periodoBpc && r.Moneda == MotorConsolidacion.MonedaLocal && r.Origen == MotorConsolidacion.OrigenCarga, registros);
        almacen.GuardarValidacion(validacion with { Importada = true });
        return new ResultadoOperacionSap(true, $"{sociedad}: {registros.Count} registros importados en {periodo}.", ResultadoEscritura.Creado, Cantidad: registros.Count);
    }

    private static ResultadoOperacionSap Convertir(JsonElement d, IAlmacenSimulado almacen)
    {
        var periodo = d.GetProperty("periodo").GetString()!;
        var periodoBpc = MotorConsolidacion.PeriodoBpc(periodo);
        var monedas = d.GetProperty("monedas").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
        var presentacion = d.GetProperty("presentacion").EnumerateArray().Select(x => x.GetString()!).ToList();
        var clases = d.GetProperty("clases").EnumerateObject().ToDictionary(p => p.Name, p => Enum.Parse<ClaseCuenta>(p.Value.GetString()!));
        decimal? Tasa(string moneda, string tipo) => almacen.Tasa(moneda, tipo, periodo);

        var nuevos = new List<RegistroBpc>();
        var cta = new List<string>();
        foreach (var (sociedad, moneda) in monedas)
        {
            var locales = almacen.Registros.Where(r => r.Entidad == sociedad && r.Periodo == periodoBpc && r.Moneda == MotorConsolidacion.MonedaLocal).ToList();
            foreach (var destino in presentacion)
            {
                var (convertidos, falta) = MotorConsolidacion.Convertir(locales, moneda, destino, clases, Tasa);
                if (falta is not null)
                {
                    // HU-30 CA4: si falta una tasa del periodo el paso falla e indica la moneda.
                    return new ResultadoOperacionSap(false, $"CURR_CONVERSION: falta la tasa {falta} para {periodo}; no se convierte {sociedad}.");
                }

                nuevos.AddRange(convertidos);
                var diferencia = convertidos.Where(r => r.Cuenta == ReglasRubros.CuentaDiferenciaConversion.Id).Sum(r => r.Importe);
                if (diferencia != 0m)
                {
                    cta.Add($"{sociedad} {destino} {ReglaCuadre.Formato(diferencia)}");
                }
            }
        }

        almacen.Reemplazar(r => r.Periodo == periodoBpc && presentacion.Contains(r.Moneda) && monedas.ContainsKey(r.Entidad) && r.Grupo.Length == 0, nuevos);
        return new ResultadoOperacionSap(true,
            $"{nuevos.Count} registros convertidos a {string.Join(" y ", presentacion)}; diferencia en conversión en {ReglasRubros.CuentaDiferenciaConversion.Id}: {(cta.Count == 0 ? "sin diferencia" : string.Join(" · ", cta))}.",
            ResultadoEscritura.Creado, Cantidad: nuevos.Count);
    }

    private static ResultadoOperacionSap EliminarIntercompania(JsonElement d, IAlmacenSimulado almacen)
    {
        var periodoBpc = MotorConsolidacion.PeriodoBpc(d.GetProperty("periodo").GetString()!);
        var grupo = d.GetProperty("grupo").GetString()!;
        var metodos = d.GetProperty("metodos").EnumerateObject().ToDictionary(p => p.Name, p => Enum.Parse<MetodoConsolidacion>(p.Value.GetString()!));
        var presentacion = d.GetProperty("presentacion").EnumerateArray().Select(x => x.GetString()!).ToList();
        var base_ = almacen.Registros.Where(r => r.Periodo == periodoBpc && presentacion.Contains(r.Moneda) && r.Grupo.Length == 0).ToList();
        var eliminaciones = MotorConsolidacion.EliminarIntercompania(base_, metodos, grupo);
        almacen.Reemplazar(r => r.Periodo == periodoBpc && r.Grupo == grupo && r.Origen == MotorConsolidacion.OrigenEliminacionIc, eliminaciones);
        var parejas = eliminaciones.Where(r => r.Cuenta != ReglasRubros.CuentaDiferenciaIntercompania.Id).Select(r => r.Entidad + "–" + r.Intco[2..]).Distinct().Count();
        return new ResultadoOperacionSap(true, $"Eliminaciones intercompañía de {grupo}: {eliminaciones.Count} registros en {parejas} partidas entre sociedades del grupo.",
            ResultadoEscritura.Creado, Cantidad: eliminaciones.Count);
    }
}
