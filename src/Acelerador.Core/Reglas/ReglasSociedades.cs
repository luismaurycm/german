using System.Globalization;
using System.Text.RegularExpressions;
using Acelerador.Core.Dominio;
using Acelerador.Sap;

namespace Acelerador.Core.Reglas;

/// <summary>Una fila de UX-05 tal como la escribe el consultor, antes de validarla.</summary>
public sealed record FilaSociedad(
    string? Codigo,
    string? Nombre,
    bool EnSap,
    string? MonedaLocal,
    string? Matriz,
    string? PlanCuentas,
    string? Porcentaje,
    string? Metodo);

/// <summary>Error de una fila (<see cref="Fila"/> desde 0) o general (<see cref="Fila"/> = -1), con el campo que marca.</summary>
public sealed record ErrorSociedad(int Fila, string? Campo, string Mensaje);

public sealed record ValidacionSociedades(IReadOnlyList<Sociedad> Sociedades, IReadOnlyList<ErrorSociedad> Errores)
{
    public bool Valida => Errores.Count == 0;
}

/// <summary>
/// Reglas de HU-09 y HU-10: solo sociedades del grupo (RN-07), plan de cuentas propio (RN-05), moneda local
/// obligatoria, % de propiedad de 0 a 100 y método inicial con una sola matriz (RN-13).
/// </summary>
public static partial class ReglasSociedades
{
    public const string CampoCodigo = "codigo";
    public const string CampoNombre = "nombre";
    public const string CampoEnSap = "ensap";
    public const string CampoMoneda = "moneda";
    public const string CampoMatriz = "matriz";
    public const string CampoPlan = "plan";
    public const string CampoPorcentaje = "porcentaje";
    public const string CampoMetodo = "metodo";

    public const string MensajeSinSociedades = "Agrega al menos una sociedad del grupo.";
    public const string MensajeSinMatriz = "Marca una sociedad con el método Matriz.";

    private static readonly CultureInfo Colombia = CultureInfo.GetCultureInfo("es-CO");

    public static string MensajeSinMoneda(string codigo) => $"La sociedad {codigo} no tiene moneda local.";

    public static string MensajeNoEsDelGrupo(string codigo) =>
        $"{codigo} no es una sociedad del grupo en el ERP. Clientes y proveedores no son entidades de consolidación (RN-07).";

    public static string MensajeRango(string codigo) => $"El % de propiedad de {codigo} debe estar entre 0 y 100.";

    public static string Metodo(MetodoConsolidacion metodo) => metodo switch
    {
        MetodoConsolidacion.Matriz => "Matriz",
        MetodoConsolidacion.IntegracionGlobal => "Integración global",
        MetodoConsolidacion.Participacion => "Participación (coligada)",
        _ => "Fuera del perímetro",
    };

    /// <summary>Lee un porcentaje con coma o punto decimal; acepta el signo menos tipográfico (−).</summary>
    public static bool TryLeerPorcentaje(string? texto, out decimal valor)
    {
        var t = (texto ?? "").Trim().Replace('−', '-').Replace("%", "", StringComparison.Ordinal).Trim();
        if (t.Contains(',', StringComparison.Ordinal) && !t.Contains('.', StringComparison.Ordinal))
        {
            return decimal.TryParse(t, NumberStyles.Number, Colombia, out valor);
        }

        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out valor);
    }

    /// <summary>Texto del porcentaje en pantalla, con coma decimal y sin ceros sobrantes.</summary>
    public static string Porcentaje(decimal valor) => valor.ToString("0.##", Colombia);

    /// <summary>
    /// Fila nueva para «Agregar sociedad». Si viene del ERP trae código, nombre, moneda y plan de cuentas. Si todavía no
    /// hay matriz, la nueva sociedad es la matriz; si la hay, la nueva queda en integración global bajo ella.
    /// </summary>
    public static FilaSociedad Nueva(SociedadErp? deErp, IEnumerable<FilaSociedad> existentes)
    {
        var matriz = existentes.FirstOrDefault(f => f.Metodo == nameof(MetodoConsolidacion.Matriz))?.Codigo?.Trim().ToUpperInvariant() ?? "";
        var metodo = matriz.Length == 0 ? MetodoConsolidacion.Matriz : MetodoConsolidacion.IntegracionGlobal;
        return deErp is null
            ? new("", "", false, "", matriz, "", "100", metodo.ToString())
            : new(deErp.Codigo, deErp.Nombre, true, deErp.Moneda, matriz, deErp.PlanCuentas, "100", metodo.ToString());
    }

    public static FilaSociedad AFila(Sociedad s) =>
        new(s.Codigo, s.Nombre, s.EnSap, s.MonedaLocal, s.Matriz, s.PlanCuentas, Porcentaje(s.PorcentajePropiedad), s.Metodo.ToString());

    /// <summary>
    /// Valida las filas y las convierte en sociedades. <paramref name="erp"/> son las sociedades del ERP: una sociedad
    /// marcada «en SAP» debe existir allí con la misma moneda y el mismo plan de cuentas.
    /// </summary>
    public static ValidacionSociedades Validar(IReadOnlyList<FilaSociedad> filas, IReadOnlyList<SociedadErp> erp)
    {
        var errores = new List<ErrorSociedad>();
        var lista = new List<Sociedad>();
        var codigos = filas.Select(f => Limpio(f.Codigo).ToUpperInvariant()).ToList();

        for (var i = 0; i < filas.Count; i++)
        {
            var f = filas[i];
            var codigo = codigos[i];
            var nombreFila = codigo.Length > 0 ? codigo : $"de la fila {i + 1}";
            void Error(string campo, string mensaje) => errores.Add(new ErrorSociedad(i, campo, mensaje));

            if (codigo.Length == 0)
            {
                Error(CampoCodigo, $"Escribe el código de la sociedad de la fila {i + 1}.");
            }
            else if (!CodigoValido().IsMatch(codigo))
            {
                Error(CampoCodigo, $"El código {codigo} debe tener de 1 a 4 letras o números.");
            }
            else if (codigos.IndexOf(codigo) != i)
            {
                Error(CampoCodigo, $"La sociedad {codigo} está repetida.");
            }

            var nombre = Limpio(f.Nombre);
            if (nombre.Length == 0)
            {
                Error(CampoNombre, $"La sociedad {nombreFila} no tiene nombre.");
            }

            var moneda = Limpio(f.MonedaLocal).ToUpperInvariant();
            if (moneda.Length == 0)
            {
                Error(CampoMoneda, MensajeSinMoneda(nombreFila));
            }
            else if (!MonedaValida().IsMatch(moneda))
            {
                Error(CampoMoneda, $"La moneda local de {nombreFila} debe ser un código ISO de 3 letras, como COP o USD.");
            }

            var plan = Limpio(f.PlanCuentas).ToUpperInvariant();
            if (f.EnSap && codigo.Length > 0)
            {
                var deErp = erp.FirstOrDefault(s => string.Equals(s.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
                if (deErp is null)
                {
                    Error(CampoCodigo, MensajeNoEsDelGrupo(codigo));
                }
                else
                {
                    if (moneda.Length > 0 && moneda != deErp.Moneda)
                    {
                        Error(CampoMoneda, $"La moneda local de {codigo} en el ERP es {deErp.Moneda}.");
                    }

                    if (plan.Length == 0)
                    {
                        Error(CampoPlan, $"La sociedad {codigo} no tiene plan de cuentas; en el ERP es {deErp.PlanCuentas}.");
                    }
                    else if (plan != deErp.PlanCuentas)
                    {
                        Error(CampoPlan, $"El plan de cuentas de {codigo} en el ERP es {deErp.PlanCuentas}.");
                    }
                }
            }

            var porcentaje = 0m;
            if (!TryLeerPorcentaje(f.Porcentaje, out porcentaje) || porcentaje < 0m || porcentaje > 100m)
            {
                Error(CampoPorcentaje, MensajeRango(nombreFila));
            }

            if (!Enum.TryParse<MetodoConsolidacion>(f.Metodo, out var metodo) || !Enum.IsDefined(metodo))
            {
                Error(CampoMetodo, $"Elige el método de consolidación de {nombreFila}.");
                metodo = MetodoConsolidacion.IntegracionGlobal;
            }

            var matriz = Limpio(f.Matriz).ToUpperInvariant();
            if (metodo == MetodoConsolidacion.Matriz)
            {
                if (matriz.Length > 0)
                {
                    Error(CampoMatriz, $"{nombreFila} tiene el método Matriz: no lleva sociedad matriz.");
                }
            }
            else if (matriz.Length == 0)
            {
                Error(CampoMatriz, $"Indica la sociedad matriz de {nombreFila}.");
            }
            else if (matriz == codigo)
            {
                Error(CampoMatriz, $"{codigo} no puede ser su propia sociedad matriz.");
            }
            else if (!codigos.Contains(matriz))
            {
                Error(CampoMatriz, $"La sociedad matriz {matriz} de {nombreFila} no está en la lista.");
            }

            lista.Add(new Sociedad
            {
                Codigo = codigo,
                Nombre = nombre,
                EnSap = f.EnSap,
                MonedaLocal = moneda,
                Matriz = matriz,
                PlanCuentas = plan,
                PorcentajePropiedad = porcentaje,
                Metodo = metodo,
            });
        }

        // HU-10 CA2: solo una sociedad del grupo puede tener el método Matriz.
        var matrices = lista.Select((s, i) => (s, i)).Where(x => x.s.Metodo == MetodoConsolidacion.Matriz).ToList();
        if (matrices.Count > 1)
        {
            var nombres = string.Join(" y ", matrices.Select(x => x.s.Codigo));
            foreach (var (_, i) in matrices)
            {
                errores.Add(new ErrorSociedad(i, CampoMetodo, $"Solo una sociedad puede tener el método Matriz: {nombres} lo tienen."));
            }
        }

        return new ValidacionSociedades(lista, errores);
    }

    /// <summary>Para continuar al mapeo hace falta al menos una sociedad y exactamente una matriz.</summary>
    public static string? BloqueoParaContinuar(IReadOnlyList<Sociedad> sociedades)
    {
        if (sociedades.Count == 0)
        {
            return MensajeSinSociedades;
        }

        return sociedades.Count(s => s.Metodo == MetodoConsolidacion.Matriz) == 1 ? null : MensajeSinMatriz;
    }

    /// <summary>HU-10 CA4: la estructura registrada es el primer periodo del modelo de propiedad (HU-32).</summary>
    public static IReadOnlyList<ParticipacionInicial> EstructuraInicial(IEnumerable<Sociedad> sociedades) =>
        sociedades.Select(s => new ParticipacionInicial(s.Codigo, s.Matriz, s.PorcentajePropiedad, s.Metodo)).ToList();

    private static string Limpio(string? texto) => (texto ?? "").Trim();

    [GeneratedRegex("^[A-Z0-9]{1,4}$")]
    private static partial Regex CodigoValido();

    [GeneratedRegex("^[A-Z]{3}$")]
    private static partial Regex MonedaValida();
}
