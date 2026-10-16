using System.Text.RegularExpressions;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Spike;

/// <summary>Los entregables documentales de HU-01 y HU-04 CA3 existen y dicen lo que deben decir.</summary>
public class EntregablesSpikeTests
{
    [Theory(DisplayName = "HU-01: el paquete Z trae las firmas de inventario, crear objeto, activar y UJBR")]
    [Trait("HU", "HU-01")]
    [InlineData("zacc_inventario/zacc_inventario.abap", "z_acc_inventario")]
    [InlineData("zacc_bw/zacc_bw.abap", "z_acc_crear_objeto")]
    [InlineData("zacc_bw/zacc_bw.abap", "z_acc_activar")]
    [InlineData("zacc_bpc/zacc_bpc.abap", "z_acc_bpc_ujbr")]
    [InlineData("zacc_bpc/zacc_bpc.abap", "z_acc_bpc_crear_miembro")]
    [InlineData("zacc_sistema/zacc_sistema.abap", "z_acc_sistema_info")]
    [InlineData("zacc_sistema/zacc_sistema.abap", "z_acc_bw_validar_fuente")]
    public void FirmasAbap(string archivo, string modulo)
    {
        var fuente = Repositorio.Leer("abap", "src", archivo);
        Assert.Matches($@"FUNCTION {modulo}\.\s+\*""-+\s+\*""\*""Interfaz local:", fuente);
        Assert.Contains("ET_MENSAJES) TYPE  BAPIRET2_T", fuente, StringComparison.Ordinal);
        Assert.Contains("PENDIENTE HU-01", fuente, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-01 CA1 a CA4: el guion trae pasos, permisos mínimos, criterios de éxito y la plantilla de la nota técnica")]
    [Trait("HU", "HU-01")]
    public void Guion()
    {
        var guion = Repositorio.Leer("docs", "05-arquitectura", "spike-HU-01.md");
        foreach (var seccion in new[] { "## 4. Permisos mínimos", "## 5. Pasos", "## 6. Criterios de éxito y de escalamiento", "## 7. Nota técnica de resultado" })
        {
            Assert.Contains(seccion, guion, StringComparison.Ordinal);
        }

        Assert.Contains("sin SAP_ALL", guion, StringComparison.Ordinal);
        Assert.Contains("UJBR", guion, StringComparison.Ordinal);
        Assert.True(Regex.Matches(guion, @"^\| P\d+ \|", RegexOptions.Multiline).Count >= 10, "El guion debe tener al menos 10 pasos.");
    }

    [Fact(DisplayName = "HU-04 CA3 (RNF-Seg-03): el rol técnico lista los permisos mínimos por paso, sin SAP_ALL")]
    [Trait("HU", "HU-04")]
    public void RolTecnico()
    {
        var rol = Repositorio.Leer("abap", "rol-ZACC_TECNICO.md");
        foreach (var paso in new[] { "Probar conexión", "Identificar arquitectura y versión", "Validar conexión ERP–BW", "Inventariar", "Crear objetos BW", "Activar objetos BW", "Respaldar BPC (UJBR)" })
        {
            Assert.Contains($"| {paso} |", rol, StringComparison.Ordinal);
        }

        Assert.Contains("**Prohibido: SAP_ALL", rol, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\| S_RFC \|[^\n]*RFC_NAME = \*", rol);
    }
}
