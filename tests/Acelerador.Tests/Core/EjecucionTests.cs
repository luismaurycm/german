using Acelerador.Core.Dominio;
using Acelerador.Core.Generacion;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

public class EjecucionTests
{
    [Fact(DisplayName = "HU-14 CA1 y HU-15 CA1: Generar ejecuta los pasos en orden; cada paso queda completo con su duración")]
    [Trait("HU", "HU-14")]
    public async Task EjecutaEnOrden()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();

        var inicio = await e.ServicioEjecucion.IniciarAsync(c.Id);
        var pendiente = (await e.Ejecuciones.ObtenerAsync(inicio.EjecucionId!.Value))!;
        Assert.All(pendiente.Pasos, p => Assert.Equal(EstadoPaso.Pendiente, p.Estado));
        Assert.Equal(EstadoEjecucion.EnCurso, pendiente.Estado);
        Assert.Equal(Pasos.Ejecucion, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);

        e.Reloj.Ahora = e.Reloj.Ahora.AddSeconds(5);
        await e.ProcesarColaAsync();
        var ej = (await e.Ejecuciones.ObtenerAsync(inicio.EjecucionId.Value))!;

        Assert.Equal(EstadoEjecucion.Completa, ej.Estado);
        var paso = Assert.Single(ej.Pasos);
        Assert.Equal(EstadoPaso.Completo, paso.Estado);
        Assert.NotNull(paso.Duracion);
        Assert.Equal(paso.Objetos.Count, paso.Creados);
        Assert.Equal("0:00", Textos.Duracion(paso.Duracion));
        Assert.Contains(e.Sap.Objetos, o => o.Operacion.NombreTecnico == GeneradorBodegaBw75.CuboSaldos && o.Sistema == SistemaSap.Bw);
        Assert.Contains(e.Sap.Objetos, o => o.Operacion.NombreTecnico == "0FI_GL_14" && o.Sistema == SistemaSap.Erp);
        var cliente = (await e.Clientes.ObtenerAsync(c.Id))!;
        Assert.Equal(EstadoCliente.Completo, cliente.Estado);
        Assert.Equal(Pasos.Reportes, cliente.PasoActual);
    }

    [Fact(DisplayName = "HU-15 CA3 y CA4: cada objeto creado queda en el registro con su nombre técnico y el paso cuenta los creados")]
    [Trait("HU", "HU-15")]
    public async Task RegistroPorObjeto()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();

        var ej = await e.GenerarAsync(c.Id);

        var reg = (await e.Registro.ListarAsync(c.Id)).Where(x => x.Paso == PasosGeneracion.Nombre(PasosGeneracion.ObjetosBw)).ToList();
        var paso = ej.Pasos[0];
        Assert.Equal(paso.Creados, reg.Count);
        foreach (var o in paso.Objetos)
        {
            Assert.Contains(reg, x => x.Objetos == $"{o.Tipo} {o.NombreTecnico}" && x.Resultado == ResultadoEjecucion.Exitoso);
        }

        Assert.Contains(reg, x => x.Objetos == "InfoCubo ZACC_C01" && x.Sistema == "SAP BW");
    }

    [Fact(DisplayName = "HU-15 CA2: con bodega existente el paso queda Existente y no llama a SAP (RN-03)")]
    [Trait("HU", "HU-15")]
    public async Task BodegaExistenteNoCrea()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.BodegaExistente = true;
        var c = await e.ClienteEnPlanAsync();

        var ej = await e.GenerarAsync(c.Id);

        Assert.Equal(EstadoPaso.Existente, ej.Pasos[0].Estado);
        Assert.Equal(0, ej.Creados);
        Assert.DoesNotContain(e.Sap.Llamadas, l => l.EsEscritura);
        var reg = Assert.Single(await e.Registro.ListarAsync(c.Id), x => x.Paso == ej.Pasos[0].Nombre);
        Assert.Contains("RN-03", reg.Mensaje, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-14 CA2: un paso con error guarda la causa, la pantalla que la corrige y detiene el plan")]
    [Trait("HU", "HU-14")]
    public async Task PasoConError()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();
        e.Opciones.FallaEscritura.Add(SistemaSap.Bw);

        var ej = await e.GenerarAsync(c.Id);

        var paso = ej.PasoConError!;
        Assert.Equal(EstadoEjecucion.ConError, ej.Estado);
        Assert.Equal("Z_ACC_CREAR_OBJETO: el usuario técnico no tiene permiso de escritura en SAP BW (Z_ACC_PASO = CREAR).", paso.Mensaje);
        Assert.Equal(Pasos.Conexiones, paso.PantallaCorrige);
        Assert.Equal(1, paso.Creados); // 0FI_GL_14 se activó en el ERP antes de fallar en BW.
        Assert.Equal(EstadoCliente.ConError, (await e.Clientes.ObtenerAsync(c.Id))!.Estado);
        Assert.Contains(await e.Registro.ListarAsync(c.Id), x => x.Resultado == ResultadoEjecucion.Fallido && x.Objetos == "0FI_GL_14");
    }

    [Fact(DisplayName = "HU-14 CA3: reintentar desde el paso fallido no repite lo completado ni duplica objetos")]
    [Trait("HU", "HU-14")]
    public async Task ReintentarSinRepetir()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();
        e.Opciones.FallaObjetos.Add(GeneradorBodegaBw75.CuboMovimientos);
        var ej = await e.GenerarAsync(c.Id);
        var creadosAntes = ej.Pasos[0].Creados;
        Assert.True(creadosAntes > 1);
        var objetosAntes = e.Sap.Objetos.Count;

        e.Opciones.FallaObjetos.Clear();
        Assert.Null((await e.ServicioEjecucion.ReintentarAsync(c.Id)).Error);
        await e.ProcesarColaAsync();
        var final = (await e.Ejecuciones.ObtenerAsync(ej.Id))!;

        Assert.Equal(EstadoEjecucion.Completa, final.Estado);
        Assert.Equal(final.Pasos[0].Objetos.Count(o => o.Estado == EstadoObjeto.Nuevo), e.Sap.Objetos.Count);
        Assert.True(e.Sap.Objetos.Count > objetosAntes);
        Assert.Equal(creadosAntes, final.Pasos[0].Existentes);
        Assert.Equal(e.Sap.Objetos.Count - objetosAntes, final.Pasos[0].Creados);
    }

    [Fact(DisplayName = "HU-14 CA3: con dos pasos, el reintento empieza en el que falló")]
    [Trait("HU", "HU-14")]
    public void ReintentoDesdeElPasoFallido()
    {
        var ej = new EjecucionPlan
        {
            Id = Guid.NewGuid(), ClienteId = Guid.NewGuid(), VarianteId = "v", Inicio = DateTimeOffset.UtcNow, Usuario = "u",
            Pasos =
            [
                new PasoEjecucion { Id = "A", Nombre = "A", Objetos = [], Estado = EstadoPaso.Completo, Creados = 3 },
                new PasoEjecucion { Id = "B", Nombre = "B", Objetos = [], Estado = EstadoPaso.Error, Mensaje = "x", PantallaCorrige = Pasos.Conexiones },
                new PasoEjecucion { Id = "C", Nombre = "C", Objetos = [] },
            ],
        };

        Assert.True(ReglasEjecucion.PrepararReintento(ej));

        Assert.Equal([EstadoPaso.Completo, EstadoPaso.Pendiente, EstadoPaso.Pendiente], ej.Pasos.Select(p => p.Estado));
        Assert.Equal(3, ej.Pasos[0].Creados);
        Assert.Null(ej.Pasos[1].Mensaje);
        Assert.Equal(2, ej.PasoActual);
        Assert.False(ReglasEjecucion.PrepararReintento(ej));
    }

    [Theory(DisplayName = "HU-14 CA2: los errores de permiso, credencial o comunicación se corrigen en Conexiones; los demás en el plan")]
    [Trait("HU", "HU-14")]
    [InlineData("Z_ACC_CREAR_OBJETO: el usuario técnico no tiene permiso de escritura en SAP BPC", "UX-02")]
    [InlineData("Partner 'bw:sapgw00' not reached", "UX-02")]
    [InlineData("Error de comunicación: no respondió en 120 s", "UX-02")]
    [InlineData("La definición del InfoCubo no es válida", "UX-08")]
    public void PantallaQueCorrige(string mensaje, string pantalla) => Assert.Equal(pantalla, ReglasEjecucion.PantallaQueCorrige(mensaje));

    [Fact(DisplayName = "Arquitectura 8.3: una llamada que agota el tiempo se reintenta una sola vez y queda en error de comunicación")]
    [Trait("HU", "HU-15")]
    public async Task TiempoAgotado()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();
        e.Opciones.LatenciaEscrituraMs = 2_000;
        var servicio = new ServicioEjecucion(e.Clientes, e.Ejecuciones, e.ServicioPlan, e.Acceso, e.Sap, e.Registro, e.Cola, e.Usuario, e.Reloj,
            new OpcionesEjecucion(TimeSpan.FromMilliseconds(100)));

        var inicio = await servicio.IniciarAsync(c.Id);
        e.Cola.Pendientes.Clear();
        await servicio.EjecutarAsync(c.Id, inicio.EjecucionId!.Value);

        var paso = (await e.Ejecuciones.ObtenerAsync(inicio.EjecucionId.Value))!.PasoConError!;
        Assert.StartsWith("Intento 2: Error de comunicación: Z_ACC_ACTIVAR no respondió en 0 s", paso.Mensaje, StringComparison.Ordinal);
        Assert.Equal(Pasos.Conexiones, paso.PantallaCorrige);
        var intentos = (await e.Registro.ListarAsync(c.Id)).Where(x => x.Objetos == "0FI_GL_14").ToList();
        Assert.Equal(2, intentos.Count);
        Assert.Equal(2, e.Sap.Llamadas.Count(l => l.EsEscritura));
    }

    [Fact(DisplayName = "Arquitectura 8.3: un error de negocio de SAP no se reintenta")]
    [Trait("HU", "HU-15")]
    public async Task ErrorDeNegocioSinReintento()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();
        e.Opciones.FallaEscritura.Add(SistemaSap.Erp);

        await e.GenerarAsync(c.Id);

        Assert.Equal(1, e.Sap.Llamadas.Count(l => l.EsEscritura));
    }

    [Fact(DisplayName = "HU-14: no se inicia una ejecución mientras otra está en curso ni si el plan no está listo")]
    [Trait("HU", "HU-14")]
    public async Task NoIniciaDosVeces()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();
        Assert.Null((await e.ServicioEjecucion.IniciarAsync(c.Id)).Error);

        Assert.Equal(ReglasEjecucion.MensajeEnCurso, (await e.ServicioEjecucion.IniciarAsync(c.Id)).Error);

        var otro = await e.ClienteAsync("Otro grupo");
        Assert.NotNull((await e.ServicioEjecucion.IniciarAsync(otro.Id)).Error);
    }

    [Fact(DisplayName = "HU-14: una ejecución interrumpida al cerrar queda con error en su paso y se puede reintentar")]
    [Trait("HU", "HU-14")]
    public async Task Interrumpida()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnPlanAsync();
        var inicio = await e.ServicioEjecucion.IniciarAsync(c.Id);

        Assert.Equal(1, await e.ServicioEjecucion.MarcarInterrumpidasAsync());

        var ej = (await e.Ejecuciones.ObtenerAsync(inicio.EjecucionId!.Value))!;
        Assert.Equal(ReglasEjecucion.MensajeInterrumpida, ej.PasoConError!.Mensaje);
        Assert.Equal(EstadoCliente.ConError, (await e.Clientes.ObtenerAsync(c.Id))!.Estado);
        Assert.Null((await e.ServicioEjecucion.ReintentarAsync(c.Id)).Error);
    }

    [Fact(DisplayName = "HU-14 y RNF-Seg-02: el mensaje del paso fallido y el registro no llevan la credencial")]
    [Trait("HU", "HU-14")]
    public async Task SinCredencial()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.InventarioVacio = true;
        var c = await e.ClienteEnPlanAsync();
        var servicio = new ServicioEjecucion(e.Clientes, e.Ejecuciones, e.ServicioPlan, e.Acceso, new EcoDeClave(e.Sap), e.Registro, e.Cola, e.Usuario, e.Reloj);

        var inicio = await servicio.IniciarAsync(c.Id);
        await servicio.EjecutarAsync(c.Id, inicio.EjecucionId!.Value);

        Assert.DoesNotContain(Entorno.Clave, (await e.Ejecuciones.ObtenerAsync(inicio.EjecucionId.Value))!.PasoConError!.Mensaje!, StringComparison.Ordinal);
        Assert.All(await e.Registro.ListarAsync(c.Id), x => Assert.DoesNotContain(Entorno.Clave, x.Mensaje, StringComparison.Ordinal));
    }

    private sealed class EcoDeClave(SimulatedSapGateway sim) : ISapGateway
    {
        public Task<ResultadoPrueba> ProbarConexionAsync(DestinoSap d, CancellationToken ct = default) => sim.ProbarConexionAsync(d, ct);

        public Task<ResultadoValidacionErpBw> ValidarConexionErpBwAsync(DestinoSap a, DestinoSap b, CancellationToken ct = default) => sim.ValidarConexionErpBwAsync(a, b, ct);

        public Task<ResultadoDeteccion> DetectarPaisajeAsync(DestinoSap a, DestinoSap b, DestinoSap c, CancellationToken ct = default) => sim.DetectarPaisajeAsync(a, b, c, ct);

        public Task<ResultadoInventario> LeerInventarioAsync(DestinoSap d, TipoInventario t, CancellationToken ct = default) => sim.LeerInventarioAsync(d, t, ct);

        public Task<ResultadoSociedades> LeerSociedadesAsync(DestinoSap d, CancellationToken ct = default) => sim.LeerSociedadesAsync(d, ct);

        public Task<ResultadoCuentas> LeerCuentasAsync(DestinoSap d, string s, CancellationToken ct = default) => sim.LeerCuentasAsync(d, s, ct);

        public Task<ResultadoOperacionSap> EjecutarAsync(DestinoSap d, OperacionSap o, CancellationToken ct = default) =>
            Task.FromResult(new ResultadoOperacionSap(false, $"logon {d.Parametros.UsuarioTecnico}/{d.Credencial.Revelar()} rechazado"));
    }
}
