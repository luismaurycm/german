using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

public class ReglasSociedadesTests
{
    private static readonly IReadOnlyList<SociedadErp> Erp = SimulatedSapGateway.SociedadesErp;

    private static List<FilaSociedad> Filas() => Entorno.FilasPrototipo();

    private static FilaSociedad Fila(string codigo, string? porcentaje = "80", string metodo = nameof(MetodoConsolidacion.IntegracionGlobal), bool enSap = false, string moneda = "COP") =>
        new(codigo, $"Sociedad {codigo}", enSap, moneda, "1000", "", porcentaje, metodo);

    [Fact(DisplayName = "HU-09 CA1: las filas del prototipo son válidas y llevan código, nombre, en SAP, moneda, matriz y plan de cuentas")]
    [Trait("HU", "HU-09")]
    public void FilasValidas()
    {
        var v = ReglasSociedades.Validar(Filas(), Erp);

        Assert.True(v.Valida, string.Join(" ", v.Errores.Select(e => e.Mensaje)));
        var s = v.Sociedades[1];
        Assert.Equal(("2000", "Filial Colombia", true, "COP", "1000", "PCCO"), (s.Codigo, s.Nombre, s.EnSap, s.MonedaLocal, s.Matriz, s.PlanCuentas));
    }

    [Fact(DisplayName = "HU-09 CA2: una sociedad en SAP que no existe en el ERP (p. ej. el tercero T901) no se registra (RN-07)")]
    [Trait("HU", "HU-09")]
    public void SoloDelGrupo()
    {
        var filas = Filas();
        filas.Add(new("T901", "Proveedor de prueba", true, "COP", "1000", "PCCO", "0", nameof(MetodoConsolidacion.FueraDelPerimetro)));

        var v = ReglasSociedades.Validar(filas, Erp);

        var e = Assert.Single(v.Errores);
        Assert.Equal((4, ReglasSociedades.CampoCodigo), (e.Fila, e.Campo));
        Assert.Equal("T901 no es una sociedad del grupo en el ERP. Clientes y proveedores no son entidades de consolidación (RN-07).", e.Mensaje);
    }

    [Fact(DisplayName = "HU-09 CA3: cada sociedad tiene su propio plan de cuentas y en SAP debe ser el del ERP (RN-05)")]
    [Trait("HU", "HU-09")]
    public void PlanPropio()
    {
        var v = ReglasSociedades.Validar(Filas(), Erp);
        Assert.Equal(["PCCO", "PCCO", "PCPA", ""], v.Sociedades.Select(s => s.PlanCuentas));

        var filas = Filas();
        filas[2] = filas[2] with { PlanCuentas = "PCCO" };
        var e = Assert.Single(ReglasSociedades.Validar(filas, Erp).Errores);
        Assert.Equal("El plan de cuentas de 3000 en el ERP es PCPA.", e.Mensaje);
    }

    [Theory(DisplayName = "HU-09 CA4: sin moneda local se muestra «La sociedad X no tiene moneda local» y no se guarda")]
    [Trait("HU", "HU-09")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MonedaObligatoria(string? moneda)
    {
        var filas = Filas();
        filas.Add(new("D500", "Filial D500", false, moneda, "1000", "", "100", nameof(MetodoConsolidacion.IntegracionGlobal)));

        var e = Assert.Single(ReglasSociedades.Validar(filas, Erp).Errores);

        Assert.Equal("La sociedad D500 no tiene moneda local.", e.Mensaje);
        Assert.Equal(ReglasSociedades.CampoMoneda, e.Campo);
    }

    [Fact(DisplayName = "HU-09: la moneda es ISO de 3 letras y en SAP coincide con la del ERP")]
    [Trait("HU", "HU-09")]
    public void MonedaValida()
    {
        var filas = Filas();
        filas[3] = filas[3] with { MonedaLocal = "PESOS" };
        filas[2] = filas[2] with { MonedaLocal = "COP" };

        var mensajes = ReglasSociedades.Validar(filas, Erp).Errores.Select(e => e.Mensaje).ToList();

        Assert.Contains("La moneda local de 9000 debe ser un código ISO de 3 letras, como COP o USD.", mensajes);
        Assert.Contains("La moneda local de 3000 en el ERP es USD.", mensajes);
    }

    [Fact(DisplayName = "HU-09: código obligatorio, de 1 a 4 caracteres y sin repetir")]
    [Trait("HU", "HU-09")]
    public void Codigos()
    {
        var filas = Filas();
        filas.Add(Fila("9000"));
        filas.Add(Fila(""));
        filas.Add(Fila("D-10"));

        var mensajes = ReglasSociedades.Validar(filas, Erp).Errores.Select(e => e.Mensaje).ToList();

        Assert.Contains("La sociedad 9000 está repetida.", mensajes);
        Assert.Contains("Escribe el código de la sociedad de la fila 6.", mensajes);
        Assert.Contains("El código D-10 debe tener de 1 a 4 letras o números.", mensajes);
    }

    [Fact(DisplayName = "HU-09 CA5 y RN-07: una sociedad no SAP no se valida contra el ERP")]
    [Trait("HU", "HU-09")]
    public void NoSapSinErp()
    {
        var v = ReglasSociedades.Validar([Fila("1000", "100", nameof(MetodoConsolidacion.Matriz)) with { Matriz = "" }, Fila("D400")], []);
        Assert.True(v.Valida, string.Join(" ", v.Errores.Select(e => e.Mensaje)));
        Assert.All(v.Sociedades, s => Assert.False(s.EnSap));
    }

    [Fact(DisplayName = "HU-10 CA1: cada sociedad tiene % de propiedad y uno de los cuatro métodos (RN-13)")]
    [Trait("HU", "HU-10")]
    public void PorcentajeYMetodo()
    {
        Assert.Equal(
            ["Matriz", "Integración global", "Participación (coligada)", "Fuera del perímetro"],
            Enum.GetValues<MetodoConsolidacion>().Select(ReglasSociedades.Metodo));

        var v = ReglasSociedades.Validar(Filas(), Erp);
        Assert.Equal([100m, 80m, 100m, 35m], v.Sociedades.Select(s => s.PorcentajePropiedad));
        Assert.Equal(MetodoConsolidacion.Participacion, v.Sociedades[3].Metodo);
    }

    [Fact(DisplayName = "HU-10 CA2: solo una sociedad del grupo puede tener el método Matriz")]
    [Trait("HU", "HU-10")]
    public void UnaSolaMatriz()
    {
        var filas = Filas();
        filas[1] = filas[1] with { Metodo = nameof(MetodoConsolidacion.Matriz), Matriz = "" };

        var errores = ReglasSociedades.Validar(filas, Erp).Errores;

        Assert.Equal([0, 1], errores.Where(e => e.Campo == ReglasSociedades.CampoMetodo).Select(e => e.Fila));
        Assert.All(errores, e => Assert.Equal("Solo una sociedad puede tener el método Matriz: 1000 y 2000 lo tienen.", e.Mensaje));
    }

    [Theory(DisplayName = "HU-10 CA3: un porcentaje fuera de 0 a 100 se marca en la fila y no se guarda")]
    [Trait("HU", "HU-10")]
    [InlineData("120")]
    [InlineData("−0,01")]
    [InlineData("-0.01")]
    [InlineData("100,01")]
    [InlineData("abc")]
    [InlineData("")]
    public void PorcentajeFueraDeRango(string porcentaje)
    {
        var filas = Filas();
        filas[1] = filas[1] with { Porcentaje = porcentaje };

        var e = Assert.Single(ReglasSociedades.Validar(filas, Erp).Errores);

        Assert.Equal((1, ReglasSociedades.CampoPorcentaje), (e.Fila, e.Campo));
        Assert.Equal("El % de propiedad de 2000 debe estar entre 0 y 100.", e.Mensaje);
    }

    [Theory(DisplayName = "HU-10 CA3: los límites 0 y 100 se aceptan, con coma o punto decimal")]
    [Trait("HU", "HU-10")]
    [InlineData("0", 0)]
    [InlineData("100", 100)]
    [InlineData("33,33", 33.33)]
    [InlineData("33.33", 33.33)]
    [InlineData("80 %", 80)]
    public void PorcentajeEnRango(string porcentaje, double esperado)
    {
        var filas = Filas();
        filas[3] = filas[3] with { Porcentaje = porcentaje };

        var v = ReglasSociedades.Validar(filas, Erp);

        Assert.True(v.Valida);
        Assert.Equal((decimal)esperado, v.Sociedades[3].PorcentajePropiedad);
    }

    [Fact(DisplayName = "HU-10 CA4: la estructura registrada es el primer periodo del modelo de propiedad (HU-32)")]
    [Trait("HU", "HU-10")]
    public void EstructuraInicial()
    {
        var estructura = ReglasSociedades.EstructuraInicial(ReglasSociedades.Validar(Filas(), Erp).Sociedades);

        Assert.Equal(
            [
                new ParticipacionInicial("1000", "", 100m, MetodoConsolidacion.Matriz),
                new ParticipacionInicial("2000", "1000", 80m, MetodoConsolidacion.IntegracionGlobal),
                new ParticipacionInicial("3000", "1000", 100m, MetodoConsolidacion.IntegracionGlobal),
                new ParticipacionInicial("9000", "1000", 35m, MetodoConsolidacion.Participacion),
            ],
            estructura);
    }

    [Fact(DisplayName = "HU-10: la matriz no lleva sociedad matriz y las demás deben tener una de la lista")]
    [Trait("HU", "HU-10")]
    public void SociedadMatriz()
    {
        var filas = Filas();
        filas[0] = filas[0] with { Matriz = "2000" };
        filas[1] = filas[1] with { Matriz = "" };
        filas[2] = filas[2] with { Matriz = "3000" };
        filas[3] = filas[3] with { Matriz = "7777" };

        var mensajes = ReglasSociedades.Validar(filas, Erp).Errores.Select(e => e.Mensaje).ToList();

        Assert.Equal(
            ["1000 tiene el método Matriz: no lleva sociedad matriz.", "Indica la sociedad matriz de 2000.", "3000 no puede ser su propia sociedad matriz.", "La sociedad matriz 7777 de 9000 no está en la lista."],
            mensajes);
    }

    [Fact(DisplayName = "HU-09 y HU-10: Agregar sociedad trae los datos del ERP; la primera es la matriz y las demás cuelgan de ella")]
    [Trait("HU", "HU-09")]
    public void AgregarDesdeErp()
    {
        var primera = ReglasSociedades.Nueva(Erp[0], []);
        Assert.Equal(new FilaSociedad("1000", "Matriz", true, "COP", "", "PCCO", "100", "Matriz"), primera);

        var segunda = ReglasSociedades.Nueva(Erp[2], [primera]);
        Assert.Equal(new FilaSociedad("3000", "Filial Panamá", true, "USD", "1000", "PCPA", "100", "IntegracionGlobal"), segunda);

        var noSap = ReglasSociedades.Nueva(null, [primera, segunda]);
        Assert.False(noSap.EnSap);
        Assert.Equal("", noSap.MonedaLocal);
    }

    [Fact(DisplayName = "HU-09: para continuar al mapeo hace falta al menos una sociedad y una matriz")]
    [Trait("HU", "HU-09")]
    public void ParaContinuar()
    {
        Assert.Equal(ReglasSociedades.MensajeSinSociedades, ReglasSociedades.BloqueoParaContinuar([]));
        Assert.Equal(ReglasSociedades.MensajeSinMatriz, ReglasSociedades.BloqueoParaContinuar([new Sociedad { Codigo = "1000" }]));
        Assert.Null(ReglasSociedades.BloqueoParaContinuar([new Sociedad { Codigo = "1000", Metodo = MetodoConsolidacion.Matriz }]));
    }
}

public class ServicioSociedadesTests
{
    [Fact(DisplayName = "HU-09 CA1: las sociedades se agregan, se editan y se conservan al recargar")]
    [Trait("HU", "HU-09")]
    public async Task GuardaYEdita()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);

        Assert.True((await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: false)).Guardado);
        var filas = Entorno.FilasPrototipo();
        filas[2] = filas[2] with { Nombre = "Filial Panamá S.A." };
        Assert.True((await e.ServicioSociedades.GuardarAsync(c.Id, filas, continuar: false)).Guardado);

        var vista = (await e.ServicioSociedades.ObtenerAsync(c.Id))!;
        Assert.Equal(["1000", "2000", "3000", "9000"], vista.Sociedades.Select(s => s.Codigo));
        Assert.Equal("Filial Panamá S.A.", vista.Sociedades[2].Nombre);
        Assert.Equal(SimulatedSapGateway.SociedadesErp, vista.SociedadesErp);
        Assert.Null(vista.ErrorErp);
    }

    [Fact(DisplayName = "HU-09 CA4: con una sociedad sin moneda no se guarda nada")]
    [Trait("HU", "HU-09")]
    public async Task SinMonedaNoGuarda()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        var filas = Entorno.FilasPrototipo();
        filas[3] = filas[3] with { MonedaLocal = "" };

        var r = await e.ServicioSociedades.GuardarAsync(c.Id, filas, continuar: true);

        Assert.False(r.Guardado);
        Assert.Contains(r.Errores, x => x.Mensaje == "La sociedad 9000 no tiene moneda local.");
        Assert.Empty(await e.Sociedades.ListarAsync(c.Id));
        Assert.Equal(Pasos.Sociedades, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);
    }

    [Fact(DisplayName = "HU-09 CA5: una sociedad marcada como no SAP habilita el paso UX-07")]
    [Trait("HU", "HU-09")]
    public async Task NoSapHabilitaUx07()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync(Entorno.FilasPrototipo().Take(3).ToList());
        Assert.False(c.TieneSociedadesNoSap);
        Assert.DoesNotContain(Pasos.Visibles(c), p => p.Codigo == Pasos.NoSap);
        Assert.Equal(Pasos.Plan, Pasos.DespuesDelMapeo(c));

        await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: false);
        c = (await e.Clientes.ObtenerAsync(c.Id))!;

        Assert.True(c.TieneSociedadesNoSap);
        Assert.Contains(Pasos.Visibles(c), p => p.Codigo == Pasos.NoSap);
        Assert.Equal(Pasos.NoSap, Pasos.DespuesDelMapeo(c));
        Assert.Equal(Pasos.NoSap, await e.ServicioMapeo.ContinuarAsync(c.Id));
        Assert.True(ReglaAvance.PuedeAbrir((await e.Clientes.ObtenerAsync(c.Id))!, Pasos.NoSap));
    }

    [Fact(DisplayName = "HU-09: Continuar al mapeo exige una matriz y avanza a UX-06")]
    [Trait("HU", "HU-09")]
    public async Task ContinuarAlMapeo()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        var r = await e.ServicioSociedades.GuardarAsync(c.Id, [], continuar: true);
        Assert.Contains(r.Errores, x => x.Mensaje == ReglasSociedades.MensajeSinSociedades);

        r = await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: true);
        Assert.True(r.Avanzo);
        Assert.Equal(Pasos.Mapeo, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);
    }

    [Fact(DisplayName = "HU-09 CA2: si no se puede leer el ERP, las sociedades en SAP no se validan ni se guardan")]
    [Trait("HU", "HU-09")]
    public async Task ErpNoResponde()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        e.Opciones.FallaSociedades = true;

        var vista = (await e.ServicioSociedades.ObtenerAsync(c.Id))!;
        var r = await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: false);

        Assert.Equal(SimulatedSapGateway.MensajeFalloSociedades, vista.ErrorErp);
        Assert.False(r.Guardado);
        Assert.Equal($"No se pudieron validar las sociedades contra el ERP: {SimulatedSapGateway.MensajeFalloSociedades}", Assert.Single(r.Errores).Mensaje);
    }

    [Fact(DisplayName = "HU-09: si la base local falla se informa «No se pudieron guardar las sociedades» con la causa")]
    [Trait("HU", "HU-09")]
    public async Task BaseFalla()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        var servicio = new ServicioSociedades(e.Clientes, new SociedadesRotas(), e.Acceso, e.Sap);

        var r = await servicio.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: false);

        Assert.False(r.Guardado);
        Assert.StartsWith("No se pudieron guardar las sociedades. La base local respondió:", r.ErrorGuardado, StringComparison.Ordinal);
        Assert.Contains("disk I/O error", r.ErrorGuardado, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-10 CA4: la estructura guardada es el primer periodo del modelo de propiedad")]
    [Trait("HU", "HU-10")]
    public async Task EstructuraGuardada()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();

        var estructura = await e.ServicioSociedades.EstructuraInicialAsync(c.Id);

        Assert.Equal(4, estructura.Count);
        Assert.Equal(new ParticipacionInicial("2000", "1000", 80m, MetodoConsolidacion.IntegracionGlobal), estructura[1]);
    }

    [Fact(DisplayName = "RN-19: a un cliente no soportado no se le registran sociedades")]
    [Trait("HU", "HU-09")]
    public async Task NoSoportado()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.B);
        var c = await e.ClienteAsync();
        foreach (var s in Enum.GetValues<SistemaSap>())
        {
            await e.ServicioConexiones.GuardarYProbarAsync(c.Id, s, Entorno.Datos(s, "s4-ejemplo.local") with { Mandante = "300" });
        }

        var r = await e.ServicioSociedades.GuardarAsync(c.Id, Entorno.FilasPrototipo(), continuar: true);

        Assert.False(r.Guardado);
        Assert.Empty(await e.Sociedades.ListarAsync(c.Id));
    }

    internal sealed class SociedadesRotas : Acelerador.Core.Puertos.ISociedadRepository
    {
        public Task<IReadOnlyList<Sociedad>> ListarAsync(Guid clienteId, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Sociedad>>([]);

        public Task GuardarAsync(Guid clienteId, IReadOnlyList<Sociedad> sociedades, CancellationToken ct = default) =>
            throw new Microsoft.Data.Sqlite.SqliteException("disk I/O error", 10);
    }
}
