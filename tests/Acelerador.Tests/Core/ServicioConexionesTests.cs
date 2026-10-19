using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Infra.Datos;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

/// <summary>Casos de uso de UX-02 contra el simulador SAP (Regla 6) y SQLite temporal.</summary>
public class ServicioConexionesTests
{
    [Fact(DisplayName = "HU-03 CA2: probar conexión exitosa guarda «Conexión exitosa», SID y producto")]
    [Trait("HU", "HU-03")]
    public async Task ConexionExitosa()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();

        var r = await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw));

        Assert.True(r.Valido);
        Assert.Equal(EstadoPrueba.Exitosa, r.Conexion!.Estado);
        Assert.Equal("BWP", r.Conexion.Sid);
        Assert.Equal("SAP BW 7.5", r.Conexion.Producto);
        Assert.Equal("RFC_ACEL", r.Conexion.UsuarioTecnico);
    }

    [Theory(DisplayName = "HU-03 CA2: probar conexión fallida guarda «Fallida» con el mensaje que devuelve SAP")]
    [Trait("HU", "HU-03")]
    [InlineData("bw-falla.local", "invalida-no", "Partner 'bw-falla.local:sapgw00' not reached")]
    [InlineData("bw-ejemplo.local", "invalida", "Name or password is incorrect (repeat logon)")]
    public async Task ConexionFallida(string host, string credencial, string mensajeSap)
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();

        var r = await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw, host, credencial));

        Assert.Equal(EstadoPrueba.Fallida, r.Conexion!.Estado);
        Assert.Equal(mensajeSap, r.Conexion.Mensaje);
        var guardada = (await e.Conexiones.ListarAsync(c.Id)).Single();
        Assert.Equal(EstadoPrueba.Fallida, guardada.Estado);
    }

    [Fact(DisplayName = "HU-03 CA1: con datos incompletos no se guarda ni se llama a SAP")]
    [Trait("HU", "HU-03")]
    public async Task DatosIncompletos()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();

        var r = await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, new DatosConexion("", "300", "", null));

        Assert.False(r.Valido);
        Assert.Equal(3, r.Errores.Count);
        Assert.Empty(await e.Conexiones.ListarAsync(c.Id));
        Assert.Empty(e.Sap.Llamadas);
    }

    [Fact(DisplayName = "HU-03 CA3: «Continuar al diagnóstico» solo avanza con las conexiones requeridas exitosas")]
    [Trait("HU", "HU-03")]
    public async Task ContinuarSoloConExito()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp));
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw, "bw-falla.local"));

        var pendientes = await e.ServicioConexiones.ContinuarADiagnosticoAsync(c.Id);
        Assert.Contains("Prueba con éxito la conexión a SAP BW.", pendientes);
        Assert.Contains("Prueba con éxito la conexión a SAP BPC.", pendientes);
        Assert.Equal(Pasos.Conexiones, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);

        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw));
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bpc, Entorno.Datos(SistemaSap.Bpc));
        await e.ServicioConexiones.ValidarErpBwAsync(c.Id);

        Assert.Empty(await e.ServicioConexiones.ContinuarADiagnosticoAsync(c.Id));
        Assert.Equal(Pasos.Diagnostico, (await e.Clientes.ObtenerAsync(c.Id))!.PasoActual);
    }

    [Fact(DisplayName = "HU-03 CA4: cada prueba de conexión queda en el registro de ejecuciones")]
    [Trait("HU", "HU-03")]
    public async Task CadaPruebaQuedaRegistrada()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp));
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw, "bw-falla.local"));
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw));

        var pruebas = (await e.Registro.ListarAsync(c.Id)).Where(x => x.Paso == ServicioConexiones.PasoProbar).ToList();

        Assert.Equal(3, pruebas.Count);
        Assert.Equal(2, pruebas.Count(x => x.Resultado == ResultadoEjecucion.Exitoso));
        var fallida = Assert.Single(pruebas, x => x.Resultado == ResultadoEjecucion.Fallido);
        Assert.Equal("SAP BW", fallida.Sistema);
        Assert.Equal(e.Usuario.Nombre, fallida.Usuario);
        Assert.Equal(e.Reloj.Ahora, fallida.Fecha);
        Assert.Contains("not reached", fallida.Mensaje, StringComparison.Ordinal);
        Assert.Equal(ServicioConexiones.SinObjetos, fallida.Objetos);
    }

    [Fact(DisplayName = "HU-03: cambiar los datos de un sistema reinicia su prueba y la detección; la validación ERP–BW solo si cambia ERP o BW")]
    [Trait("HU", "HU-03")]
    public async Task CambioReinicia()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ConectarTodoAsync(c.Id);
        await e.ServicioConexiones.ValidarErpBwAsync(c.Id);
        Assert.NotNull((await e.Clientes.ObtenerAsync(c.Id))!.Deteccion);

        e.Opciones.SistemasConFallo.Add(SistemaSap.Bpc);
        var r = await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bpc, Entorno.Datos(SistemaSap.Bpc) with { Mandante = "200" });

        var cliente = (await e.Clientes.ObtenerAsync(c.Id))!;
        Assert.Equal(EstadoPrueba.Fallida, r.Conexion!.Estado);
        Assert.Null(cliente.Deteccion);
        Assert.NotNull(cliente.Validacion);

        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw, credencial: "otra-clave"));
        Assert.Null((await e.Clientes.ObtenerAsync(c.Id))!.Validacion);
    }

    [Fact(DisplayName = "HU-04 CA2: la credencial va al almacén; SQLite solo guarda la referencia")]
    [Trait("HU", "HU-04")]
    public async Task CredencialEnAlmacen()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ConectarTodoAsync(c.Id);
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw, "bw-falla.local"));

        var conexiones = await e.Conexiones.ListarAsync(c.Id);
        Assert.All(conexiones, x => Assert.Equal(ServicioConexiones.ReferenciaCredencial(c.Id, x.Sistema), x.ReferenciaCredencial));
        Assert.All(conexiones, x => Assert.Equal(Entorno.Clave, e.Credenciales.Leer(x.ReferenciaCredencial!)!.Revelar()));

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        var bytes = await File.ReadAllBytesAsync(e.Base.RutaArchivo);
        Assert.False(Contiene(bytes, Entorno.Clave), "La base SQLite contiene la credencial en UTF-8.");
        Assert.False(Contiene(bytes, Entorno.Clave, System.Text.Encoding.Unicode), "La base SQLite contiene la credencial en UTF-16.");
        Assert.All(await e.Registro.ListarAsync(c.Id), x => Assert.DoesNotContain(Entorno.Clave, x.Mensaje, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "HU-04 CA4: si SAP devuelve la clave en el error, se reporta sin escribirla")]
    [Trait("HU", "HU-04")]
    public async Task ErrorSinCredencial()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.EcoDeCredencialEnErrores = true;
        var c = await e.ClienteAsync();

        var r = await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp));

        Assert.Equal(EstadoPrueba.Fallida, r.Conexion!.Estado);
        Assert.StartsWith("Name or password is incorrect", r.Conexion.Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain(Entorno.Clave, r.Conexion.Mensaje, StringComparison.Ordinal);
        Assert.Contains(CredencialSap.Mascara, r.Conexion.Mensaje, StringComparison.Ordinal);
        Assert.DoesNotContain(Entorno.Clave, (await e.Registro.ListarAsync(c.Id)).Single().Mensaje, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-04: si la credencial no está en el almacén (p. ej. otro equipo), la prueba falla con un mensaje claro")]
    [Trait("HU", "HU-04")]
    public async Task CredencialPerdida()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp));
        e.Credenciales.Eliminar(ServicioConexiones.ReferenciaCredencial(c.Id, SistemaSap.Erp));

        var r = await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp, credencial: null));

        Assert.Equal(EstadoPrueba.Fallida, r.Conexion!.Estado);
        Assert.Equal(ServicioConexiones.MensajeCredencialNoDisponible, r.Conexion.Mensaje);
    }

    [Fact(DisplayName = "HU-05 CA2: la validación ERP–BW queda guardada y registrada con fecha, usuario y mensaje")]
    [Trait("HU", "HU-05")]
    public async Task ValidacionRegistrada()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ConectarTodoAsync(c.Id);

        var r = await e.ServicioConexiones.ValidarErpBwAsync(c.Id);

        Assert.True(r.Exitosa);
        var v = (await e.Clientes.ObtenerAsync(c.Id))!.Validacion!;
        Assert.True(v.Exitosa);
        Assert.Equal(e.Reloj.Ahora, v.Fecha);
        Assert.Equal(e.Usuario.Nombre, v.Usuario);
        Assert.Contains("ECPCLNT300", v.Mensaje, StringComparison.Ordinal);

        var reg = Assert.Single(await e.Registro.ListarAsync(c.Id), x => x.Paso == ServicioConexiones.PasoValidarErpBw);
        Assert.Equal(e.Usuario.Nombre, reg.Usuario);
        Assert.Equal(e.Reloj.Ahora, reg.Fecha);
        Assert.Equal(v.Mensaje, reg.Mensaje);
    }

    [Fact(DisplayName = "HU-05 CA3: si la validación falla, se guarda el mensaje del sistema y no se puede continuar")]
    [Trait("HU", "HU-05")]
    public async Task ValidacionFallida()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp));
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw, "bw-sinfuente.local"));
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bpc, Entorno.Datos(SistemaSap.Bpc));

        var r = await e.ServicioConexiones.ValidarErpBwAsync(c.Id);

        Assert.False(r.Exitosa);
        Assert.Equal("El sistema fuente ECPCLNT300 no está activo en BW (transacción RSA1).", r.Mensaje);
        Assert.Equal(["Valida con éxito la conexión ERP–BW."], await e.ServicioConexiones.ContinuarADiagnosticoAsync(c.Id));
        Assert.Equal(ResultadoEjecucion.Fallido, (await e.Registro.ListarAsync(c.Id)).First(x => x.Paso == ServicioConexiones.PasoValidarErpBw).Resultado);
    }

    [Fact(DisplayName = "HU-05 CA1: en arquitectura B (ERP y BW en el mismo sistema) la validación no aplica")]
    [Trait("HU", "HU-05")]
    public async Task ValidacionNoAplicaEnB()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.B);
        var c = await e.ClienteAsync();
        foreach (var s in Enum.GetValues<SistemaSap>())
        {
            await e.ServicioConexiones.GuardarYProbarAsync(c.Id, s, Entorno.Datos(s, "s4-ejemplo.local") with { Mandante = "300" });
        }

        var vista = (await e.ServicioConexiones.ObtenerAsync(c.Id))!;
        Assert.False(vista.AplicaValidacionErpBw);
        Assert.False((await e.ServicioConexiones.ValidarErpBwAsync(c.Id)).Exitosa);
        Assert.DoesNotContain(e.Sap.Llamadas, l => l.Operacion == "Z_ACC_BW_VALIDAR_FUENTE");
    }

    [Theory(DisplayName = "HU-06 CA1 y CA4: al conectar los tres sistemas se detectan y guardan la arquitectura y las versiones")]
    [Trait("HU", "HU-06")]
    [InlineData(PerfilSimulado.ABw75, Arquitectura.A, ProductoBw.Bw75, ProductoBpc.Bpc101, "A-BW75-BPC101-STD")]
    [InlineData(PerfilSimulado.ABw4Hana, Arquitectura.A, ProductoBw.Bw4Hana, ProductoBpc.Bpc2021, "A-BW4H-BPC2021-STD")]
    public async Task DeteccionGuardada(PerfilSimulado perfil, Arquitectura arq, ProductoBw bw, ProductoBpc bpc, string variante)
    {
        await using var e = await Entorno.CrearAsync(perfil);
        var c = await e.ClienteAsync();

        await e.ConectarTodoAsync(c.Id);

        // Se relee con un repositorio nuevo para comprobar que quedó en la base, no solo en memoria.
        var guardado = (await new SqliteClienteRepository(new BaseLocal(e.Base.RutaArchivo)).ObtenerAsync(c.Id))!;
        Assert.Equal(arq, guardado.Deteccion!.Arquitectura);
        Assert.Equal(bw, guardado.Deteccion.Bw.Producto);
        Assert.Equal(bpc, guardado.Deteccion.Bpc.Producto);
        Assert.Equal(variante, guardado.Deteccion.VarianteId);
        Assert.Equal(EstadoCliente.EnCurso, guardado.Estado);

        var reg = Assert.Single(await e.Registro.ListarAsync(c.Id), x => x.Paso == ServicioConexiones.PasoDetectar);
        Assert.Equal(ResultadoEjecucion.Exitoso, reg.Resultado);
    }

    [Fact(DisplayName = "HU-06 CA3: versión sin variante: «Versión no soportada aún», cliente no soportado y no se continúa")]
    [Trait("HU", "HU-06")]
    public async Task VersionNoSoportada()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.AVersionNoSoportada);
        var c = await e.ClienteAsync();
        await e.ConectarTodoAsync(c.Id);
        await e.ServicioConexiones.ValidarErpBwAsync(c.Id);

        var cliente = (await e.Clientes.ObtenerAsync(c.Id))!;
        Assert.Equal(EstadoCliente.NoSoportado, cliente.Estado);
        Assert.StartsWith("Versión no soportada aún", cliente.MotivoEstado, StringComparison.Ordinal);
        Assert.Equal(["Versión no soportada aún."], await e.ServicioConexiones.ContinuarADiagnosticoAsync(c.Id));
    }

    [Fact(DisplayName = "HU-06: si la detección falla, queda registrada y se informa el mensaje")]
    [Trait("HU", "HU-06")]
    public async Task DeteccionFallida()
    {
        await using var e = await Entorno.CrearAsync();
        e.Opciones.FallaDeteccion = true;
        var c = await e.ClienteAsync();
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp));
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bw, Entorno.Datos(SistemaSap.Bw));

        var r = await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bpc, Entorno.Datos(SistemaSap.Bpc));

        Assert.Equal(SimulatedSapGateway.MensajeFalloDeteccion, r.ErrorDeteccion);
        Assert.Null((await e.Clientes.ObtenerAsync(c.Id))!.Deteccion);
        Assert.Equal(ResultadoEjecucion.Fallido, (await e.Registro.ListarAsync(c.Id)).First(x => x.Paso == ServicioConexiones.PasoDetectar).Resultado);
    }

    [Fact(DisplayName = "HU-57 CA2, CA3 y CA4: arquitectura B: no soportado con motivo, sin escrituras en SAP y con registro")]
    [Trait("HU", "HU-57")]
    public async Task ArquitecturaB()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.B);
        var c = await e.ClienteAsync();
        foreach (var s in Enum.GetValues<SistemaSap>())
        {
            await e.ServicioConexiones.GuardarYProbarAsync(c.Id, s, Entorno.Datos(s, "s4-ejemplo.local") with { Mandante = "300" });
        }

        // CA3: estado y motivo en el cliente.
        var cliente = (await e.Clientes.ObtenerAsync(c.Id))!;
        Assert.Equal(Arquitectura.B, cliente.Deteccion!.Arquitectura);
        Assert.Equal(EstadoCliente.NoSoportado, cliente.Estado);
        Assert.Equal("Arquitectura B no soportada en la versión 1 (RN-19).", cliente.MotivoEstado);

        // CA2: no se puede continuar ni configurar, y nada escribió en el cliente.
        Assert.Equal(["Arquitectura B no soportada en la versión 1."], await e.ServicioConexiones.ContinuarADiagnosticoAsync(c.Id));
        var modulos = await e.ServicioModulos.GuardarAsync(c.Id, [Modulo.Consolidacion], continuar: true);
        Assert.False(modulos.Guardado);
        Assert.Empty((await e.Clientes.ObtenerAsync(c.Id))!.Modulos);
        Assert.False((await e.ServicioDiagnostico.GuardarCamposZAsync(c.Id, [], continuar: true)).Exitosa);
        Assert.DoesNotContain(e.Sap.Llamadas, l => l.EsEscritura);

        // CA4: la detección está en el registro.
        var reg = Assert.Single(await e.Registro.ListarAsync(c.Id), x => x.Paso == ServicioConexiones.PasoDetectar);
        Assert.Equal(ResultadoEjecucion.NoSoportado, reg.Resultado);
        Assert.Contains("Arquitectura B no soportada en la versión 1", reg.Mensaje, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-57: si una nueva detección encuentra arquitectura A, el cliente vuelve a estar en curso")]
    [Trait("HU", "HU-57")]
    public async Task ReDeteccion()
    {
        await using var e = await Entorno.CrearAsync(PerfilSimulado.B);
        var c = await e.ClienteAsync();
        await e.ConectarTodoAsync(c.Id);
        Assert.Equal(EstadoCliente.NoSoportado, (await e.Clientes.ObtenerAsync(c.Id))!.Estado);

        e.Opciones = new OpcionesSimulador { Perfil = PerfilSimulado.ABw75 };
        await e.ServicioConexiones.DetectarAsync(c.Id);

        var cliente = (await e.Clientes.ObtenerAsync(c.Id))!;
        Assert.Equal(EstadoCliente.EnCurso, cliente.Estado);
        Assert.Null(cliente.MotivoEstado);
    }

    private static bool Contiene(byte[] datos, string texto, System.Text.Encoding? codificacion = null)
    {
        var buscado = (codificacion ?? System.Text.Encoding.UTF8).GetBytes(texto);
        return datos.AsSpan().IndexOf(buscado) >= 0;
    }
}
