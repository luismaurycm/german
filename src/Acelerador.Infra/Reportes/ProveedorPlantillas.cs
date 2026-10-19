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
