using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace Acelerador.Infra.Reportes;

/// <summary>Estilo de una celda de las plantillas: los colores son los del prototipo de UX-11 a UX-15.</summary>
public enum EstiloCelda : uint
{
    Normal = 0,
    Titulo = 1,
    Encabezado = 2,
    Editable = 3,
    Total = 4,
    Nota = 5,
    Parametro = 6,
}

/// <summary>Una celda: valor de texto o número, fórmula opcional (p. ej. EPM) y estilo.</summary>
public sealed record Celda(string Referencia, object? Valor, EstiloCelda Estilo = EstiloCelda.Normal, string? Formula = null);

/// <summary>Lista desplegable de validación de datos (p. ej. el método en C02 o la moneda en UX-11).</summary>
public sealed record ListaValidacion(string Rango, IReadOnlyList<string> Opciones);

/// <summary>Una hoja con sus celdas, listas desplegables, anchos de columna y nombres definidos.</summary>
public sealed class HojaExcel(string nombre)
{
    public string Nombre { get; } = nombre;

    public List<Celda> Celdas { get; } = [];

    public List<ListaValidacion> Listas { get; } = [];

    public Dictionary<int, double> Anchos { get; } = [];

    public HojaExcel Poner(string referencia, object? valor, EstiloCelda estilo = EstiloCelda.Normal, string? formula = null)
    {
        Celdas.Add(new Celda(referencia, valor, estilo, formula));
        return this;
    }
}

/// <summary>
/// Escritor mínimo de .xlsx con Open XML SDK, sin Excel instalado (ADR-08). Lo usan las plantillas C01 a C04, los reportes
/// EPM y los formularios de captura. Las fórmulas EPM se escriben tal cual: las resuelve el EPM add-in del cliente.
/// </summary>
public static class LibroExcel
{
    public static byte[] Escribir(IReadOnlyList<HojaExcel> hojas, IReadOnlyDictionary<string, string>? nombres = null)
    {
        using var memoria = new MemoryStream();
        using (var doc = SpreadsheetDocument.Create(memoria, SpreadsheetDocumentType.Workbook))
        {
            var libro = doc.AddWorkbookPart();
            libro.Workbook = new Workbook();
            var estilos = libro.AddNewPart<WorkbookStylesPart>();
            estilos.Stylesheet = Estilos();
            var listaHojas = libro.Workbook.AppendChild(new Sheets());
            uint id = 1;
            foreach (var hoja in hojas)
            {
                var parte = libro.AddNewPart<WorksheetPart>();
                var datos = new SheetData();
                var ws = new Worksheet();
                if (hoja.Anchos.Count > 0)
                {
                    var columnas = new Columns();
                    foreach (var (col, ancho) in hoja.Anchos.OrderBy(a => a.Key))
                    {
                        columnas.Append(new Column { Min = (uint)col, Max = (uint)col, Width = ancho, CustomWidth = true });
                    }

                    ws.Append(columnas);
                }

                ws.Append(datos);
                foreach (var fila in hoja.Celdas.GroupBy(c => Fila(c.Referencia)).OrderBy(g => g.Key))
                {
                    var row = new Row { RowIndex = fila.Key };
                    foreach (var c in fila.OrderBy(c => Columna(c.Referencia)))
                    {
                        row.Append(CrearCelda(c));
                    }

                    datos.Append(row);
                }

                if (hoja.Listas.Count > 0)
                {
                    var validaciones = new DataValidations();
                    foreach (var l in hoja.Listas)
                    {
                        validaciones.Append(new DataValidation
                        {
                            Type = DataValidationValues.List,
                            AllowBlank = true,
                            ShowErrorMessage = true,
                            SequenceOfReferences = new ListValue<StringValue> { InnerText = l.Rango },
                            Formula1 = new Formula1($"\"{string.Join(",", l.Opciones)}\""),
                        });
                    }

                    validaciones.Count = (uint)hoja.Listas.Count;
                    ws.Append(validaciones);
                }

                parte.Worksheet = ws;
                listaHojas.Append(new Sheet { Id = libro.GetIdOfPart(parte), SheetId = id++, Name = hoja.Nombre });
            }

            if (nombres is { Count: > 0 })
            {
                var definidos = new DefinedNames();
                foreach (var (nombre, rango) in nombres)
                {
                    definidos.Append(new DefinedName { Name = nombre, Text = rango });
                }

                libro.Workbook.Append(definidos);
            }

            libro.Workbook.Save();
        }

        return memoria.ToArray();
    }

    private static Cell CrearCelda(Celda c)
    {
        var celda = new Cell { CellReference = c.Referencia, StyleIndex = (uint)c.Estilo };
        if (c.Formula is not null)
        {
            celda.CellFormula = new CellFormula(c.Formula);
        }

        switch (c.Valor)
        {
            case null:
                break;
            case decimal or int or double:
                celda.DataType = CellValues.Number;
                celda.CellValue = new CellValue(Convert.ToDecimal(c.Valor, System.Globalization.CultureInfo.InvariantCulture));
                break;
            default:
                celda.DataType = CellValues.InlineString;
                celda.InlineString = new InlineString(new Text(c.Valor.ToString()!));
                break;
        }

        return celda;
    }

    private static Stylesheet Estilos() => new(
        new Fonts(
            new Font(new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
            new Font(new Bold(), new FontSize { Val = 14 }, new Color { Rgb = "FF365F91" }, new FontName { Val = "Calibri" }),
            new Font(new Bold(), new FontSize { Val = 11 }, new Color { Rgb = "FFFFFFFF" }, new FontName { Val = "Calibri" }),
            new Font(new Bold(), new FontSize { Val = 11 }, new FontName { Val = "Calibri" }),
            new Font(new Italic(), new FontSize { Val = 9 }, new Color { Rgb = "FF4A5568" }, new FontName { Val = "Calibri" })),
        new Fills(
            new Fill(new PatternFill { PatternType = PatternValues.None }),
            new Fill(new PatternFill { PatternType = PatternValues.Gray125 }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FF144E8C" }) { PatternType = PatternValues.Solid }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFFFF4C2" }) { PatternType = PatternValues.Solid }),
            new Fill(new PatternFill(new ForegroundColor { Rgb = "FFE3ECF7" }) { PatternType = PatternValues.Solid })),
        new Borders(
            new Border(),
            new Border(new LeftBorder { Style = BorderStyleValues.Thin }, new RightBorder { Style = BorderStyleValues.Thin }, new TopBorder { Style = BorderStyleValues.Thin }, new BottomBorder { Style = BorderStyleValues.Thin }, new DiagonalBorder())),
        new CellFormats(
            new CellFormat(),
            new CellFormat { FontId = 1, ApplyFont = true },
            new CellFormat { FontId = 2, FillId = 2, BorderId = 1, ApplyFont = true, ApplyFill = true, ApplyBorder = true },
            new CellFormat { FillId = 3, BorderId = 1, ApplyFill = true, ApplyBorder = true, NumberFormatId = 4, ApplyNumberFormat = true },
            new CellFormat { FontId = 3, BorderId = 1, ApplyFont = true, ApplyBorder = true, NumberFormatId = 4, ApplyNumberFormat = true },
            new CellFormat { FontId = 4, ApplyFont = true },
            new CellFormat { FillId = 4, BorderId = 1, ApplyFill = true, ApplyBorder = true }));

    private static uint Fila(string referencia) => uint.Parse(new string(referencia.SkipWhile(char.IsLetter).ToArray()), System.Globalization.CultureInfo.InvariantCulture);

    private static string Columna(string referencia) => new string(referencia.TakeWhile(char.IsLetter).ToArray()).PadLeft(3);
}
