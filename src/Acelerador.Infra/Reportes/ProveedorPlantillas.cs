using System.Text.Json;
using Acelerador.Core.Puertos;

namespace Acelerador.Infra.Reportes;

/// <summary>Plantillas EPM del acelerador. Cada sprint agrega las suyas; un identificador desconocido es un error de programación.</summary>
public sealed class ProveedorPlantillas : IProveedorPlantillas
{
    public byte[] Generar(string plantilla, string parametrosJson)
    {
        using var doc = JsonDocument.Parse(parametrosJson);
        return plantilla switch
        {
            "C02" => PlantillaC02.Generar(doc.RootElement),
            "C01" or "C03" or "C04" or "AJ" => PlantillasCierre.Generar(plantilla, doc.RootElement),
            "R" => PlantillaReporte.Generar(doc.RootElement),
            _ => throw new ArgumentOutOfRangeException(nameof(plantilla), plantilla, "Plantilla desconocida."),
        };
    }
}

/// <summary>
/// C02 Participaciones y métodos (UX-13, RF-31): parámetros grupo y periodo, y por sociedad % control, % consolidación,
/// % propiedad (editables) y método (lista), conectados al modelo de propiedad de BPC por la conexión EPM.
/// </summary>
public static class PlantillaC02
{
    public static readonly string[] Metodos = ["Matriz", "Integración global", "Participación", "Fuera del perímetro"];

    public static byte[] Generar(JsonElement p)
    {
        var conexion = p.GetProperty("conexion").GetString()!;
        var grupo = p.GetProperty("grupo").GetString()!;
        var periodo = p.GetProperty("periodo").GetString()!;
        var sociedades = p.GetProperty("sociedades").EnumerateArray().Select(s => (Codigo: s.GetProperty("codigo").GetString()!, Nombre: s.GetProperty("nombre").GetString()!)).ToList();

        var h = new HojaExcel("C02");
        h.Anchos[1] = 14;
        h.Anchos[2] = 30;
        for (var c = 3; c <= 7; c++)
        {
            h.Anchos[c] = 18;
        }

        h.Poner("A1", "[Logo del cliente]", EstiloCelda.Nota)
            .Poner("B1", "C02 Participaciones y métodos", EstiloCelda.Titulo)
            .Poner("A2", "ID: C02 · Modelo de propiedad (PROPIEDAD)", EstiloCelda.Nota)
            .Poner("A3", "Grupo", EstiloCelda.Encabezado).Poner("B3", grupo, EstiloCelda.Parametro)
            .Poner("A4", "Periodo", EstiloCelda.Encabezado).Poner("B4", periodo, EstiloCelda.Parametro)
            .Poner("A6", "Sociedad", EstiloCelda.Encabezado).Poner("B6", "Nombre", EstiloCelda.Encabezado)
            .Poner("C6", "% control", EstiloCelda.Encabezado).Poner("D6", "% consolidación", EstiloCelda.Encabezado)
            .Poner("E6", "% propiedad", EstiloCelda.Encabezado).Poner("F6", "Método", EstiloCelda.Encabezado)
            .Poner("G6", "Cambio frente al periodo anterior", EstiloCelda.Encabezado);

        var fila = 7;
        foreach (var (codigo, nombre) in sociedades)
        {
            h.Poner($"A{fila}", codigo).Poner($"B{fila}", nombre);
            foreach (var (col, cuenta) in new[] { ("C", "PCTRL"), ("D", "PCON"), ("E", "POWN"), ("F", "METHOD") })
            {
                h.Poner($"{col}{fila}", null, EstiloCelda.Editable, $"EPMRetrieveData(\"{conexion}\",\"{cuenta}\",$B$3,$B$4,$A{fila})");
            }

            h.Poner($"G{fila}", null, EstiloCelda.Normal, $"IF(E{fila}<>EPMRetrieveData(\"{conexion}\",\"POWN\",$B$3,EPMPrevPeriod($B$4),$A{fila}),\"% propiedad cambió\",\"\")");
            fila++;
        }

        h.Listas.Add(new ListaValidacion($"F7:F{fila - 1}", Metodos));
        h.Poner($"A{fila + 1}", "Porcentajes de 0 a 100 y método obligatorio (RN-13). Un cambio aplica desde el periodo elegido y no altera los anteriores (RN-14). Guardar datos escribe en el modelo de propiedad de BPC; cada cambio queda en la auditoría de BPC (RNF-Aud-02).", EstiloCelda.Nota);
        h.Poner($"A{fila + 2}", "Fuente: Modelo BPC PROPIEDAD.", EstiloCelda.Nota);

        return LibroExcel.Escribir([h], new Dictionary<string, string> { ["EPM_CONEXION"] = $"C02!$B$3" });
    }
}

/// <summary>
/// Plantillas del cierre (UX-11, UX-14, UX-15): C01 Conciliación intercompañía, C03 Validación de cuadre, C04 Estado del
/// cierre y Asientos de ajuste. Todas leen el modelo de consolidación de BPC por la conexión EPM con el grupo y el periodo.
/// </summary>
public static class PlantillasCierre
{
    public static byte[] Generar(string id, JsonElement p)
    {
        var conexion = p.GetProperty("conexion").GetString()!;
        var grupo = p.GetProperty("grupo").GetString()!;
        var periodo = p.GetProperty("periodo").GetString()!;
        var monedas = p.GetProperty("monedas").EnumerateArray().Select(m => m.GetString()!).ToList();
        var umbral = p.GetProperty("umbral").GetDecimal();
        var sociedades = p.GetProperty("sociedades").EnumerateArray().Select(s => (Codigo: s.GetProperty("codigo").GetString()!, Nombre: s.GetProperty("nombre").GetString()!)).ToList();

        var (titulo, columnas, nota) = id switch
        {
            "C01" => ("C01 Conciliación intercompañía", new[] { "Sociedad", "Contraparte", "Cuentas", "Registra la sociedad", "Registra la contraparte", "Diferencia", "Estado" },
                $"Umbral {umbral.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("es-CO"))} (DAF, pregunta 14). Solo sociedades del grupo (RN-07). Se puede consultar antes de la consolidación."),
            "C03" => ("C03 Validación de cuadre", new[] { "Sociedad", "Nombre", "Activo − (pasivo + patrimonio)", "Utilidad EERR − resultado balance", "Cuentas mapeadas", "Resultado" },
                "Una sociedad solo se importa si cuadra y tiene todas sus cuentas con saldo mapeadas (RN-16)."),
            "C04" => ("C04 Estado del cierre", new[] { "Sociedad", "Carga", "Cuadre", "Conciliación IC", "Conversión", "Consolidación", "Reportes", "Bloqueo" },
                "Bloquear una sociedad impide cargas, asientos y reprocesos del periodo (RN-17). Cada bloqueo queda en la auditoría de BPC (RNF-Aud-02)."),
            _ => ("Asientos de ajuste", new[] { "Sociedad", "Cuenta", "Intercompañía", "Débito", "Crédito", "Observación" },
                "Guardar asiento solo si los débitos son iguales a los créditos (RN-15). No se aceptan líneas de sociedades bloqueadas (RN-17)."),
        };

        var h = new HojaExcel(id);
        h.Anchos[1] = 14;
        for (var c = 2; c <= columnas.Length; c++)
        {
            h.Anchos[c] = 20;
        }

        h.Poner("A1", "[Logo del cliente]", EstiloCelda.Nota)
            .Poner("B1", titulo, EstiloCelda.Titulo)
            .Poner("A2", $"ID: {id} · Modelo de consolidación ({conexion})", EstiloCelda.Nota)
            .Poner("A3", "Grupo", EstiloCelda.Encabezado).Poner("B3", grupo, EstiloCelda.Parametro)
            .Poner("A4", "Periodo", EstiloCelda.Encabezado).Poner("B4", periodo, EstiloCelda.Parametro);
        if (id is "C01" or "AJ")
        {
            h.Poner("C3", "Moneda", EstiloCelda.Encabezado).Poner("D3", monedas.FirstOrDefault() ?? "", EstiloCelda.Parametro);
            h.Listas.Add(new ListaValidacion("D3", monedas));
        }

        for (var c = 0; c < columnas.Length; c++)
        {
            h.Poner($"{(char)('A' + c)}6", columnas[c], EstiloCelda.Encabezado);
        }

        var fila = 7;
        if (id is "C03" or "C04")
        {
            foreach (var (codigo, nombre) in sociedades)
            {
                h.Poner($"A{fila}", codigo);
                for (var c = 1; c < columnas.Length; c++)
                {
                    var col = (char)('A' + c);
                    h.Poner($"{col}{fila}", c == 1 && id == "C03" ? nombre : null, EstiloCelda.Normal,
                        c == 1 && id == "C03" ? null : $"EPMRetrieveData(\"{conexion}\",\"{id}_{columnas[c]}\",$B$3,$B$4,$A{fila})");
                }

                fila++;
            }
        }
        else
        {
            for (var c = 0; c < columnas.Length; c++)
            {
                h.Poner($"{(char)('A' + c)}{fila}", null, id == "AJ" ? EstiloCelda.Editable : EstiloCelda.Normal,
                    id == "AJ" ? null : $"EPMRetrieveData(\"{conexion}\",\"{id}_{columnas[c]}\",$B$3,$B$4,$D$3)");
            }

            fila++;
        }

        h.Poner($"A{fila + 1}", nota, EstiloCelda.Nota);
        h.Poner($"A{fila + 2}", $"Fuente: Modelo BPC {conexion}.", EstiloCelda.Nota);
        return LibroExcel.Escribir([h], new Dictionary<string, string> { ["EPM_CONEXION"] = $"{id}!$B$3" });
    }
}

/// <summary>
/// Estructura común de los reportes R01 a R20 (UX-11, HU-42): logo, título e ID, parámetros Sociedad, Incluir filiales, Moneda
/// y Periodo en celdas con lista, y el cuerpo con rubros (código y descripción) cuyo valor trae EPM del modelo de consolidación
/// como la suma de las cuentas mapeadas (RN-10). Con Incluir filiales = Sí, EPM expande una columna por filial y el total (RN-09).
/// </summary>
public static class PlantillaReporte
{
    public static byte[] Generar(JsonElement p)
    {
        var id = p.GetProperty("id").GetString()!;
        var nombre = p.GetProperty("nombre").GetString()!;
        var conexion = p.GetProperty("conexion").GetString()!;
        var periodo = p.GetProperty("periodo").GetString()!;
        var monedas = p.GetProperty("monedas").EnumerateArray().Select(m => m.GetString()!).ToList();
        var sociedades = p.GetProperty("sociedades").EnumerateArray().Select(s => s.GetProperty("codigo").GetString()!).ToList();
        var encabezados = p.GetProperty("encabezados").EnumerateArray().Select(e => e.GetString()!).ToList();

        var h = new HojaExcel(id);
        h.Anchos[1] = 16;
        h.Anchos[2] = 44;
        for (var c = 3; c <= 6; c++)
        {
            h.Anchos[c] = 20;
        }

        h.Poner("A1", "[Logo del cliente]", EstiloCelda.Nota)
            .Poner("B1", $"{id} {nombre}", EstiloCelda.Titulo)
            .Poner("A2", $"ID: {id} · Fuente: Modelo BPC {conexion}", EstiloCelda.Nota)
            .Poner("A4", "Sociedad", EstiloCelda.Encabezado).Poner("B4", sociedades.FirstOrDefault() ?? "", EstiloCelda.Parametro)
            .Poner("A5", "Incluir filiales", EstiloCelda.Encabezado).Poner("B5", "Sí", EstiloCelda.Parametro)
            .Poner("A6", "Moneda", EstiloCelda.Encabezado).Poner("B6", monedas.FirstOrDefault() ?? "", EstiloCelda.Parametro)
            .Poner("A7", "Periodo", EstiloCelda.Encabezado).Poner("B7", periodo, EstiloCelda.Parametro);
        h.Listas.Add(new ListaValidacion("B4", sociedades));
        h.Listas.Add(new ListaValidacion("B5", ["Sí", "No"]));
        h.Listas.Add(new ListaValidacion("B6", monedas));

        var columnas = Math.Min(encabezados.Count, 5);
        for (var c = 0; c < columnas; c++)
        {
            h.Poner($"{(char)('A' + c)}9", encabezados[c], EstiloCelda.Encabezado);
        }

        var valor = (char)('A' + columnas);
        h.Poner($"{valor}9", "Valor", EstiloCelda.Encabezado);
        var fila = 10;
        foreach (var f in p.GetProperty("filas").EnumerateArray())
        {
            var tipo = f.GetProperty("tipo").GetString();
            var etiquetas = f.GetProperty("etiquetas").EnumerateArray().Select(e => e.GetString() ?? "").ToList();
            var estilo = tipo is "Seccion" or "Subtotal" or "Total" ? EstiloCelda.Total : EstiloCelda.Normal;
            for (var c = 0; c < Math.Min(etiquetas.Count, columnas); c++)
            {
                h.Poner($"{(char)('A' + c)}{fila}", etiquetas[c], estilo);
            }

            var miembro = f.TryGetProperty("rubro", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : tipo == "Detalle" ? etiquetas.FirstOrDefault() : null;
            if (!string.IsNullOrEmpty(miembro) && tipo is "Rubro" or "Detalle" or "Total")
            {
                h.Poner($"{valor}{fila}", null, estilo, $"EPMRetrieveData(\"{conexion}\",\"{miembro}\",$B$4,$B$5,$B$6,$B$7)");
            }

            fila++;
        }

        if (fila == 10)
        {
            h.Poner("A10", "Reporte dinámico: EPM expande las filas al actualizar.", EstiloCelda.Nota);
            fila++;
        }

        h.Poner($"A{fila + 1}", "Cada valor es la suma de las cuentas mapeadas al rubro (RN-10). Moneda local o dólares (RN-06). Incluir filiales agrega una columna por filial y el total (RN-09).", EstiloCelda.Nota);
        return LibroExcel.Escribir([h], new Dictionary<string, string> { ["EPM_CONEXION"] = $"{id}!$B$4" });
    }
}
