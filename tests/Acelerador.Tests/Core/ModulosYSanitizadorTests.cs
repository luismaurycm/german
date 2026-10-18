using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-08")]
public class ReglasModulosTests
{
    [Fact(DisplayName = "HU-08 CA1: hay una ficha por módulo con la descripción de lo que genera")]
    public void Catalogo()
    {
        Assert.Equal(Enum.GetValues<Modulo>(), CatalogoModulos.Todos.Select(f => f.Modulo));
        Assert.All(CatalogoModulos.Todos, f => Assert.False(string.IsNullOrWhiteSpace(f.Genera)));
        Assert.Equal(34, CatalogoModulos.Todos.Sum(f => f.Reportes));
    }

    [Theory(DisplayName = "HU-08 CA2: el resumen cuenta módulos y reportes")]
    [InlineData(new Modulo[0], 0, 0, "0 módulos · 0 reportes")]
    [InlineData(new[] { Modulo.Consolidacion }, 1, 24, "1 módulo · 24 reportes")]
    [InlineData(new[] { Modulo.ActivosFijos, Modulo.Deterioros }, 2, 5, "2 módulos · 5 reportes")]
    [InlineData(new[] { Modulo.Consolidacion, Modulo.Presupuesto, Modulo.ActivosFijos, Modulo.Deterioros }, 4, 34, "4 módulos · 34 reportes")]
    [InlineData(new[] { Modulo.Presupuesto, Modulo.Presupuesto }, 1, 5, "1 módulo · 5 reportes")]
    public void Resumen(Modulo[] seleccion, int modulos, int reportes, string texto)
    {
        var r = ReglasModulos.Resumen(seleccion);
        Assert.Equal(modulos, r.Modulos);
        Assert.Equal(reportes, r.Reportes);
        Assert.Equal(texto, r.Texto);
    }

    [Fact(DisplayName = "HU-08 CA3: sin módulos marcados no se puede continuar")]
    public void SinModulos()
    {
        Assert.False(ReglasModulos.PuedeContinuar([]));
        Assert.True(ReglasModulos.PuedeContinuar([Modulo.Deterioros]));
    }

    [Theory(DisplayName = "HU-08 CA4 (RN-12): activos fijos y deterioros se quedan en BW")]
    [InlineData(Modulo.ActivosFijos, true)]
    [InlineData(Modulo.Deterioros, true)]
    [InlineData(Modulo.Consolidacion, false)]
    [InlineData(Modulo.Presupuesto, false)]
    public void SoloBw(Modulo modulo, bool soloBw)
    {
        var f = CatalogoModulos.Ficha(modulo);
        Assert.Equal(!soloBw, f.CreaModeloBpc);
        Assert.Equal(soloBw ? "Se queda en BW; no crea modelo en BPC." : null, f.Nota);
    }
}

[Trait("HU", "HU-04")]
public class SanitizadorMensajesTests
{
    [Fact(DisplayName = "HU-04 CA4: la credencial exacta se reemplaza por la máscara")]
    public void SecretoExacto()
    {
        var cred = new CredencialSap("S3creto!");
        var limpio = SanitizadorMensajes.Limpiar("Logon failed for RFC_ACEL with S3creto! on BWP", cred);
        Assert.DoesNotContain("S3creto!", limpio, StringComparison.Ordinal);
        Assert.Contains(CredencialSap.Mascara, limpio, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "HU-04 CA4: los patrones password/clave = valor se enmascaran aunque no se conozca el secreto")]
    [InlineData("PASSWD=abc123 USER=X")]
    [InlineData("password: abc123")]
    [InlineData("la clave=abc123 no es válida")]
    public void Patrones(string mensaje) =>
        Assert.DoesNotContain("abc123", SanitizadorMensajes.Limpiar(mensaje), StringComparison.Ordinal);

    [Fact(DisplayName = "HU-04 CA4: un mensaje sin secretos queda igual")]
    public void SinSecretos() =>
        Assert.Equal("Name or password is incorrect (repeat logon)", SanitizadorMensajes.Limpiar("Name or password is incorrect (repeat logon)", new CredencialSap("x9")));

    [Fact(DisplayName = "RNF-Seg-02: la credencial no sale por ToString ni por interpolación")]
    public void CredencialNoSeImprime()
    {
        var cred = new CredencialSap("S3creto!");
        var destino = new DestinoSap(SistemaSap.Bw, new ParametrosConexion("h", "100", "U"), cred);
        Assert.DoesNotContain("S3creto!", $"{cred} {destino}", StringComparison.Ordinal);
    }
}
