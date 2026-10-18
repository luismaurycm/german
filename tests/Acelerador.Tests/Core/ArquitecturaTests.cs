using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-06")]
public class ClasificadorArquitecturaTests
{
    private static Clasificacion Clasificar(PerfilSimulado perfil) =>
        ClasificadorArquitectura.Clasificar(SimulatedSapGateway.Paisaje(perfil), CatalogoVariantes.VersionUno);

    [Fact(DisplayName = "HU-06 CA2: arquitectura A reconoce BW 7.5 con BPC 10.1 modelo estándar (InfoCubos)")]
    public void Bw75()
    {
        var c = Clasificar(PerfilSimulado.ABw75);
        Assert.Equal(Arquitectura.A, c.Arquitectura);
        Assert.True(c.Soportada);
        Assert.Equal(CatalogoVariantes.Bw75Bpc101, c.Variante);
        Assert.Equal(TipoInfoProvider.InfoCubo, c.Variante!.InfoProvider);
        Assert.Equal(ClasificadorArquitectura.TituloVarianteDisponible, c.Titulo);
    }

    [Fact(DisplayName = "HU-06 CA2 (RNF-Comp-05): arquitectura A reconoce BW/4HANA con BPC 2021 modelo estándar y genera aDSO, no InfoCubos")]
    public void Bw4Hana()
    {
        var c = Clasificar(PerfilSimulado.ABw4Hana);
        Assert.Equal(Arquitectura.A, c.Arquitectura);
        Assert.True(c.Soportada);
        Assert.Equal(TipoInfoProvider.Adso, c.Variante!.InfoProvider);
        Assert.Contains("aDSO", c.Explicacion, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "HU-06 CA3: sin variante disponible se muestra «Versión no soportada aún»")]
    [InlineData(PerfilSimulado.AVersionNoSoportada)]
    [InlineData(PerfilSimulado.ABpcEmbebido)]
    public void SinVariante(PerfilSimulado perfil)
    {
        var c = Clasificar(perfil);
        Assert.Equal(Arquitectura.A, c.Arquitectura);
        Assert.False(c.Soportada);
        Assert.Null(c.Variante);
        Assert.Equal("Versión no soportada aún", c.Titulo);
    }

    [Fact(DisplayName = "HU-57 CA1 (RN-19): ERP y BW en el mismo sistema es arquitectura B y no se soporta en la versión 1")]
    public void ArquitecturaB()
    {
        var c = Clasificar(PerfilSimulado.B);
        Assert.Equal(Arquitectura.B, c.Arquitectura);
        Assert.False(c.Soportada);
        Assert.Equal("Arquitectura B no soportada en la versión 1", c.Titulo);
        Assert.Contains("no se configura ni se genera", c.Explicacion, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-06 CA2: aunque las versiones coincidan con una variante, el mismo SID en ERP y BW es arquitectura B")]
    public void BPrevalece()
    {
        var p = SimulatedSapGateway.Paisaje(PerfilSimulado.ABw75) with { SidErp = "S4H", SidBw = "S4H" };
        Assert.Equal(Arquitectura.B, ClasificadorArquitectura.Clasificar(p, CatalogoVariantes.VersionUno).Arquitectura);
    }

    [Fact(DisplayName = "HU-06 CA4: la descripción de lo guardado en el cliente coincide con la clasificación original")]
    public void Describir()
    {
        foreach (var perfil in Enum.GetValues<PerfilSimulado>())
        {
            var p = SimulatedSapGateway.Paisaje(perfil);
            var original = ClasificadorArquitectura.Clasificar(p, CatalogoVariantes.VersionUno);
            var guardada = new DeteccionArquitectura(original.Arquitectura, p.Bw, p.Bpc, original.Variante?.Id, DateTimeOffset.UnixEpoch);
            Assert.Equal(original, ClasificadorArquitectura.Describir(guardada, CatalogoVariantes.VersionUno));
        }
    }

    [Fact(DisplayName = "RNF-Comp-04 (ADR-07): agregar una variante no cambia la resolución de las existentes")]
    public void AgregarVarianteNoTocaExistentes()
    {
        var nueva = new VarianteGeneracion("A-BW75-BPC101-EMB", "BW 7.5 + BPC 10.1 embebido", ProductoBw.Bw75, ProductoBpc.Bpc101, ModeloBpc.Embebido, TipoInfoProvider.InfoCubo);
        var ampliado = new CatalogoVariantes([.. CatalogoVariantes.VersionUno.Variantes, nueva]);

        foreach (var perfil in new[] { PerfilSimulado.ABw75, PerfilSimulado.ABw4Hana })
        {
            var p = SimulatedSapGateway.Paisaje(perfil);
            Assert.Equal(CatalogoVariantes.VersionUno.Buscar(p.Bw, p.Bpc), ampliado.Buscar(p.Bw, p.Bpc));
        }

        var embebido = SimulatedSapGateway.Paisaje(PerfilSimulado.ABpcEmbebido);
        Assert.Null(CatalogoVariantes.VersionUno.Buscar(embebido.Bw, embebido.Bpc));
        Assert.Equal(nueva, ampliado.Buscar(embebido.Bw, embebido.Bpc));
    }

    [Theory(DisplayName = "HU-06 CA1: textos de versión que se muestran en UX-02 y UX-01")]
    [InlineData(PerfilSimulado.ABw75, "A · BW 7.5 + BPC 10.1 modelo estándar")]
    [InlineData(PerfilSimulado.ABw4Hana, "A · BW/4HANA 2.0 + BPC 2021 modelo estándar")]
    [InlineData(PerfilSimulado.B, "B · S/4HANA, BW y BPC embebidos")]
    public void TextoArquitectura(PerfilSimulado perfil, string esperado)
    {
        var p = SimulatedSapGateway.Paisaje(perfil);
        var c = ClasificadorArquitectura.Clasificar(p, CatalogoVariantes.VersionUno);
        Assert.Equal(esperado, Textos.Arquitectura(new DeteccionArquitectura(c.Arquitectura, p.Bw, p.Bpc, c.Variante?.Id, DateTimeOffset.UnixEpoch)));
    }
}

[Trait("HU", "HU-03")]
public class ReglaAvanceTests
{
    private static Cliente NuevoCliente() => new() { Id = Guid.NewGuid(), Nombre = "X", Creado = DateTimeOffset.UnixEpoch, CreadoPor = "x" };

    private static List<Conexion> Conexiones(EstadoPrueba erp, EstadoPrueba bw, EstadoPrueba bpc) =>
    [
        new() { ClienteId = Guid.Empty, Sistema = SistemaSap.Erp, Host = "erp", Mandante = "300", Estado = erp, Sid = "ECP" },
        new() { ClienteId = Guid.Empty, Sistema = SistemaSap.Bw, Host = "bw", Mandante = "100", Estado = bw, Sid = "BWP" },
        new() { ClienteId = Guid.Empty, Sistema = SistemaSap.Bpc, Host = "bw", Mandante = "100", Estado = bpc, Sid = "BWP" },
    ];

    private static DeteccionArquitectura Deteccion(PerfilSimulado perfil)
    {
        var p = SimulatedSapGateway.Paisaje(perfil);
        var c = ClasificadorArquitectura.Clasificar(p, CatalogoVariantes.VersionUno);
        return new DeteccionArquitectura(c.Arquitectura, p.Bw, p.Bpc, c.Variante?.Id, DateTimeOffset.UnixEpoch);
    }

    [Fact(DisplayName = "HU-03 CA3: sin conexiones exitosas no se puede continuar al diagnóstico")]
    public void SinConexiones()
    {
        var pendientes = ReglaAvance.PendientesParaDiagnostico(NuevoCliente(), Conexiones(EstadoPrueba.Exitosa, EstadoPrueba.Fallida, EstadoPrueba.SinProbar), CatalogoVariantes.VersionUno);
        Assert.Contains("Prueba con éxito la conexión a SAP BW.", pendientes);
        Assert.Contains("Prueba con éxito la conexión a SAP BPC.", pendientes);
        Assert.DoesNotContain("Prueba con éxito la conexión a SAP ERP.", pendientes);
    }

    [Fact(DisplayName = "HU-05 CA3: con ERP y BW separados, falta la validación ERP–BW")]
    public void FaltaValidacion()
    {
        var c = NuevoCliente();
        c.Deteccion = Deteccion(PerfilSimulado.ABw75);
        var lista = Conexiones(EstadoPrueba.Exitosa, EstadoPrueba.Exitosa, EstadoPrueba.Exitosa);

        Assert.Equal(["Valida con éxito la conexión ERP–BW."], ReglaAvance.PendientesParaDiagnostico(c, lista, CatalogoVariantes.VersionUno));

        c.Validacion = new ValidacionErpBw(false, DateTimeOffset.UnixEpoch, "u", "falló");
        Assert.Equal(["Valida con éxito la conexión ERP–BW."], ReglaAvance.PendientesParaDiagnostico(c, lista, CatalogoVariantes.VersionUno));

        c.Validacion = new ValidacionErpBw(true, DateTimeOffset.UnixEpoch, "u", "ok");
        Assert.Empty(ReglaAvance.PendientesParaDiagnostico(c, lista, CatalogoVariantes.VersionUno));
    }

    [Fact(DisplayName = "HU-06 CA3: con versión sin variante no se puede continuar")]
    public void VersionNoSoportada()
    {
        var c = NuevoCliente();
        c.Deteccion = Deteccion(PerfilSimulado.AVersionNoSoportada);
        c.Validacion = new ValidacionErpBw(true, DateTimeOffset.UnixEpoch, "u", "ok");
        var pendientes = ReglaAvance.PendientesParaDiagnostico(c, Conexiones(EstadoPrueba.Exitosa, EstadoPrueba.Exitosa, EstadoPrueba.Exitosa), CatalogoVariantes.VersionUno);
        Assert.Equal(["Versión no soportada aún."], pendientes);
    }

    [Fact(DisplayName = "HU-57 CA2: con arquitectura B no se puede continuar")]
    public void ArquitecturaB()
    {
        var c = NuevoCliente();
        c.Deteccion = Deteccion(PerfilSimulado.B);
        var lista = Conexiones(EstadoPrueba.Exitosa, EstadoPrueba.Exitosa, EstadoPrueba.Exitosa);
        lista.ForEach(x => x.Sid = "S4H");
        Assert.Equal(["Arquitectura B no soportada en la versión 1."], ReglaAvance.PendientesParaDiagnostico(c, lista, CatalogoVariantes.VersionUno));
    }

    [Fact(DisplayName = "HU-06: con todo exitoso pero sin detectar, falta identificar la arquitectura")]
    public void FaltaDeteccion()
    {
        var c = NuevoCliente();
        c.Validacion = new ValidacionErpBw(true, DateTimeOffset.UnixEpoch, "u", "ok");
        Assert.Equal(["Identifica la arquitectura y la versión."],
            ReglaAvance.PendientesParaDiagnostico(c, Conexiones(EstadoPrueba.Exitosa, EstadoPrueba.Exitosa, EstadoPrueba.Exitosa), CatalogoVariantes.VersionUno));
    }

    [Fact(DisplayName = "RN-19: un cliente no soportado solo abre Conexiones")]
    public void PuedeAbrir()
    {
        var c = NuevoCliente();
        c.AvanzarA(Pasos.Modulos);
        Assert.True(ReglaAvance.PuedeAbrir(c, Pasos.Modulos));
        Assert.False(ReglaAvance.PuedeAbrir(c, Pasos.Sociedades));

        c.Estado = EstadoCliente.NoSoportado;
        Assert.True(ReglaAvance.PuedeAbrir(c, Pasos.Conexiones));
        Assert.False(ReglaAvance.PuedeAbrir(c, Pasos.Diagnostico));
        Assert.False(ReglaAvance.PuedeAbrir(c, Pasos.Modulos));
    }
}
