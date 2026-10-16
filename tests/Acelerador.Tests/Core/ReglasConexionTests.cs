using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-03")]
public class ReglasConexionTests
{
    private static readonly DatosConexion Completos = new("bw-ejemplo.local", "100", "RFC_ACEL", "x");

    [Fact(DisplayName = "HU-03 CA1: con todos los datos no hay errores")]
    public void Completo() => Assert.Empty(ReglasConexion.Validar(Completos, tieneCredencialGuardada: false));

    [Theory(DisplayName = "HU-03 CA1: host, mandante, usuario técnico y credencial son obligatorios")]
    [InlineData(ReglasConexion.CampoHost)]
    [InlineData(ReglasConexion.CampoMandante)]
    [InlineData(ReglasConexion.CampoUsuario)]
    [InlineData(ReglasConexion.CampoCredencial)]
    public void CadaCampoObligatorio(string campo)
    {
        var datos = campo switch
        {
            ReglasConexion.CampoHost => Completos with { Host = " " },
            ReglasConexion.CampoMandante => Completos with { Mandante = "" },
            ReglasConexion.CampoUsuario => Completos with { UsuarioTecnico = null },
            _ => Completos with { CredencialNueva = "" },
        };

        var errores = ReglasConexion.Validar(datos, tieneCredencialGuardada: false);

        Assert.Equal([campo], errores.Keys);
        Assert.EndsWith("obligatorio.", errores[campo].Replace("obligatoria.", "obligatorio.", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "HU-03 CA1: con credencial guardada no hay que volver a escribirla")]
    public void CredencialGuardada() =>
        Assert.Empty(ReglasConexion.Validar(Completos with { CredencialNueva = null }, tieneCredencialGuardada: true));

    [Theory(DisplayName = "HU-03 CA1: el mandante tiene tres dígitos")]
    [InlineData("10")]
    [InlineData("1000")]
    [InlineData("1a0")]
    public void MandanteInvalido(string mandante) =>
        Assert.Contains(ReglasConexion.CampoMandante, ReglasConexion.Validar(Completos with { Mandante = mandante }, false).Keys);

    [Fact(DisplayName = "HU-03 CA1: el usuario de SAP tiene máximo 12 caracteres")]
    public void UsuarioLargo() =>
        Assert.Contains(ReglasConexion.CampoUsuario, ReglasConexion.Validar(Completos with { UsuarioTecnico = "USUARIO_MUY_LARGO" }, false).Keys);

    [Fact(DisplayName = "HU-03: cambiar los datos invalida la prueba anterior")]
    public void Cambio()
    {
        var c = new Conexion { ClienteId = Guid.NewGuid(), Sistema = SistemaSap.Bw, Host = "bw-ejemplo.local", Mandante = "100", UsuarioTecnico = "RFC_ACEL" };
        Assert.False(ReglasConexion.Cambio(c, Completos with { CredencialNueva = null, UsuarioTecnico = "rfc_acel" }));
        Assert.True(ReglasConexion.Cambio(c, Completos with { CredencialNueva = null, Mandante = "200" }));
        Assert.True(ReglasConexion.Cambio(c, Completos with { CredencialNueva = "nueva" }));
    }
}

[Trait("HU", "HU-05")]
public class ReglaRn01Tests
{
    private static Conexion C(SistemaSap s, string host, string mandante, string? sid = null) =>
        new() { ClienteId = Guid.Empty, Sistema = s, Host = host, Mandante = mandante, Sid = sid };

    [Fact(DisplayName = "HU-05 CA1 (RN-01): con SID distintos, ERP y BW están separados y aplica la validación")]
    public void SidDistintos() =>
        Assert.True(ReglaRn01.AplicaValidacionErpBw(C(SistemaSap.Erp, "a", "300", "ECP"), C(SistemaSap.Bw, "a", "300", "BWP")));

    [Fact(DisplayName = "HU-05 CA1 (RN-01): con el mismo SID es el mismo sistema y no aplica")]
    public void MismoSid() =>
        Assert.False(ReglaRn01.AplicaValidacionErpBw(C(SistemaSap.Erp, "a", "300", "S4H"), C(SistemaSap.Bw, "b", "100", "S4H")));

    [Fact(DisplayName = "HU-05 CA1 (RN-01): sin SID, el mismo host y mandante es el mismo sistema")]
    public void MismoHostYMandante() =>
        Assert.False(ReglaRn01.AplicaValidacionErpBw(C(SistemaSap.Erp, "S4-EJEMPLO.local", "300"), C(SistemaSap.Bw, "s4-ejemplo.local", "300")));

    [Theory(DisplayName = "HU-05 CA1 (RN-01): sin SID, otro host u otro mandante son sistemas separados")]
    [InlineData("erp.local", "300", "bw.local", "300")]
    [InlineData("s4.local", "300", "s4.local", "100")]
    public void Separados(string h1, string m1, string h2, string m2) =>
        Assert.True(ReglaRn01.AplicaValidacionErpBw(C(SistemaSap.Erp, h1, m1), C(SistemaSap.Bw, h2, m2)));

    [Fact(DisplayName = "HU-05: la validación solo se lanza con ERP y BW conectados")]
    public void PuedeValidar()
    {
        var erp = C(SistemaSap.Erp, "a", "300");
        var bw = C(SistemaSap.Bw, "b", "100");
        erp.Estado = EstadoPrueba.Exitosa;
        Assert.False(ReglaRn01.PuedeValidar(erp, bw));
        bw.Estado = EstadoPrueba.Exitosa;
        Assert.True(ReglaRn01.PuedeValidar(erp, bw));
    }
}
