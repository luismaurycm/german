using System.Text.RegularExpressions;
using Acelerador.Core.Dominio;

namespace Acelerador.Core.Reglas;

/// <summary>Error de una versión (<see cref="Fila"/> desde 0) o general (<see cref="Fila"/> = -1).</summary>
public sealed record ErrorVersion(int Fila, string Mensaje);

/// <summary>Reglas de HU-12: versión de presupuesto anual obligatoria y códigos de versión sin repetir.</summary>
public static partial class ReglasVersiones
{
    public const int LongitudCodigo = 20;
    public const string MensajeSinAnual = "Debe existir la versión de presupuesto anual.";

    /// <summary>Quita espacios y pasa el código a mayúsculas, como lo guarda BPC.</summary>
    public static IReadOnlyList<VersionPresupuesto> Normalizar(IEnumerable<VersionPresupuesto> versiones) =>
        versiones.Select(v => v with { Codigo = (v.Codigo ?? "").Trim().ToUpperInvariant(), Nombre = (v.Nombre ?? "").Trim() }).ToList();

    public static IReadOnlyList<ErrorVersion> Validar(IReadOnlyList<VersionPresupuesto> versiones)
    {
        var errores = new List<ErrorVersion>();
        if (!versiones.Any(v => v.Anual))
        {
            errores.Add(new ErrorVersion(-1, MensajeSinAnual));
        }

        for (var i = 0; i < versiones.Count; i++)
        {
            var v = versiones[i];
            if (v.Codigo.Length == 0)
            {
                errores.Add(new ErrorVersion(i, $"Escribe el código de la versión de la fila {i + 1}."));
            }
            else if (!CodigoValido().IsMatch(v.Codigo))
            {
                errores.Add(new ErrorVersion(i, $"El código {v.Codigo} solo admite letras, números y guion bajo (hasta {LongitudCodigo})."));
            }
            else if (versiones.Take(i).Any(o => o.Codigo == v.Codigo))
            {
                errores.Add(new ErrorVersion(i, $"El código de versión {v.Codigo} está repetido."));
            }

            if (v.Nombre.Length == 0)
            {
                errores.Add(new ErrorVersion(i, $"La versión {(v.Codigo.Length > 0 ? v.Codigo : $"de la fila {i + 1}")} no tiene nombre."));
            }
        }

        return errores;
    }

    /// <summary>HU-12 CA4: las versiones pasan al plan de generación solo si el cliente tiene el módulo Presupuesto.</summary>
    public static IReadOnlyList<VersionPresupuesto> ParaPlan(Cliente cliente) =>
        cliente.Modulos.Contains(Modulo.Presupuesto) ? cliente.Versiones.ToList() : [];

    [GeneratedRegex("^[A-Z0-9_]{1,20}$")]
    private static partial Regex CodigoValido();
}
