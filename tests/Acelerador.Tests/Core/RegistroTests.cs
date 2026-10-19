using System.Text;
using Acelerador.Core.Dominio;
using Acelerador.Core.Reglas;
using Acelerador.Core.Servicios;
using Acelerador.Sap;
using Acelerador.Tests.Soporte;

namespace Acelerador.Tests.Core;

[Trait("HU", "HU-54")]
public class RegistroTests
{
    private static Ejecucion E(DateTimeOffset fecha, Guid cliente, string paso = "Probar conexión", ResultadoEjecucion r = ResultadoEjecucion.Exitoso, string mensaje = "ok") =>
        new(fecha, @"CM\consultor1", cliente, "SAP ERP", paso, "Ninguno (solo lectura)", r, mensaje);

    private static string Texto(byte[] csv) => Encoding.UTF8.GetString(csv);

    [Fact(DisplayName = "HU-54 CA1: el registro tiene fecha y hora, usuario, sistema, paso, objetos y resultado")]
    public void Columnas() =>
        Assert.Equal(["Fecha y hora", "Usuario", "Sistema", "Paso", "Objetos", "Resultado", "Detalle"], ReglasRegistro.Columnas);

    [Fact(DisplayName = "HU-54 CA2: se conserva al menos 12 meses; la depuración solo quita lo que pasó de 12 meses")]
    public async Task Retencion()
    {
        await using var e = await Entorno.CrearAsync();
        var ahora = e.Reloj.Ahora;
        var c = Guid.NewGuid();
        var casi = ahora.AddMonths(-12).AddDays(1);
        var justo = ahora.AddMonths(-12);
        var pasado = ahora.AddMonths(-12).AddDays(-1);
        foreach (var f in new[] { ahora, casi, justo, pasado })
        {
            await e.Registro.RegistrarAsync(E(f, c, mensaje: f.ToString("O")));
        }

        var depuradas = await e.ServicioRegistro.DepurarAsync();

        Assert.Equal(1, depuradas);
        Assert.Equal([ahora, casi, justo], (await e.Registro.ListarAsync(c)).Select(x => x.Fecha));
        Assert.True(ReglasRegistro.SeConserva(E(justo, c), ahora));
        Assert.False(ReglasRegistro.SeConserva(E(pasado, c), ahora));
        Assert.Equal(12, ReglasRegistro.MesesRetencion);
    }

    [Fact(DisplayName = "HU-54 CA2: el 100 % de las ejecuciones del recorrido queda registrado con sus campos")]
    public async Task TodoRegistrado()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnModulosAsync();
        e.Opciones.FallaInventario = TipoInventario.CamposZ;
        await e.ServicioDiagnostico.EscanearAsync(c.Id);

        var reg = await e.Registro.ListarAsync(c.Id);

        // 3 pruebas de conexión + validación ERP–BW + detección + 4 pasos del escaneo bueno + 4 del escaneo con error en el último.
        Assert.Equal(13, reg.Count);
        Assert.Equal(3, reg.Count(x => x.Paso == ServicioConexiones.PasoProbar));
        Assert.Single(reg, x => x.Paso == ServicioConexiones.PasoValidarErpBw);
        Assert.Single(reg, x => x.Paso == ServicioConexiones.PasoDetectar);
        Assert.Single(reg, x => x.Resultado == ResultadoEjecucion.Fallido && x.Paso == "Inventario: Campos Z");
        Assert.All(reg, x =>
        {
            Assert.Equal(@"CM\consultor1", x.Usuario);
            Assert.False(string.IsNullOrEmpty(x.Sistema));
            Assert.False(string.IsNullOrEmpty(x.Objetos));
        });
    }

    [Fact(DisplayName = "HU-54 CA3: Descargar registro entrega un CSV con los mismos campos y filas")]
    public async Task Descarga()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync("Grupo Ejemplo Andino");
        await e.ConectarTodoAsync(c.Id);

        var archivo = (await e.ServicioRegistro.ExportarAsync(c.Id))!;
        var lineas = Texto(archivo.Contenido).TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.StartsWith("registro-ejecuciones-grupo-ejemplo-andino-", archivo.Nombre, StringComparison.Ordinal);
        Assert.EndsWith(".csv", archivo.Nombre, StringComparison.Ordinal);
        Assert.Equal([0xEF, 0xBB, 0xBF], archivo.Contenido.Take(3));
        Assert.Equal("\"Fecha y hora\";\"Usuario\";\"Sistema\";\"Paso\";\"Objetos\";\"Resultado\";\"Detalle\"", lineas[0]);
        var reg = await e.Registro.ListarAsync(c.Id);
        Assert.Equal(reg.Count + 1, lineas.Length);
        var ultima = reg[^1];
        var fecha = ultima.Fecha.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal($"\"{fecha}\";\"{ultima.Usuario}\";\"SAP ERP\";\"Probar conexión\";\"Ninguno (solo lectura)\";\"Exitoso\";\"{ultima.Mensaje}\"", lineas[^1]);
    }

    [Fact(DisplayName = "HU-54 CA3: el CSV escapa comillas y neutraliza fórmulas de Excel")]
    public void CsvSeguro()
    {
        var csv = Texto(ReglasRegistro.Csv([E(DateTimeOffset.UtcNow, Guid.NewGuid(), paso: "=HYPERLINK(\"x\")", mensaje: "dijo \"hola\"; adiós")]));

        Assert.Contains("\"'=HYPERLINK(\"\"x\"\")\"", csv, StringComparison.Ordinal);
        Assert.Contains("\"dijo \"\"hola\"\"; adiós\"", csv, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-54: la consulta filtra por resultado")]
    public async Task Filtro()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteAsync();
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp, "erp-falla.local"));
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Erp, Entorno.Datos(SistemaSap.Erp));

        var todas = (await e.ServicioRegistro.ObtenerAsync(c.Id, null))!;
        var fallidas = (await e.ServicioRegistro.ObtenerAsync(c.Id, ResultadoEjecucion.Fallido))!;

        Assert.Equal(2, todas.Total);
        Assert.Equal(2, fallidas.Total);
        Assert.Single(fallidas.Ejecuciones);
        Assert.Equal(ResultadoEjecucion.Fallido, fallidas.Ejecuciones[0].Resultado);
    }

    [Fact(DisplayName = "HU-54 CA4: ni el registro ni su descarga contienen la credencial (RNF-Seg-02)")]
    public async Task SinCredenciales()
    {
        await using var e = await Entorno.CrearAsync();
        var c = await e.ClienteEnMapeoAsync();
        e.Opciones.EcoDeCredencialEnErrores = true;
        await e.ServicioConexiones.GuardarYProbarAsync(c.Id, SistemaSap.Bpc, Entorno.Datos(SistemaSap.Bpc));
        await e.ServicioMapeo.AsignarAsync(c.Id, "1000", "F-00-101", ["11050501"]);

        var reg = await e.Registro.ListarAsync(c.Id);
        var csv = Texto((await e.ServicioRegistro.ExportarAsync(c.Id))!.Contenido);

        Assert.True(reg.Count >= 10);
        Assert.Contains(reg, x => x.Mensaje.Contains(CredencialSap.Mascara, StringComparison.Ordinal));
        Assert.All(reg, x => Assert.DoesNotContain(Entorno.Clave, string.Join('|', x.Usuario, x.Sistema, x.Paso, x.Objetos, x.Mensaje), StringComparison.Ordinal));
        Assert.DoesNotContain(Entorno.Clave, csv, StringComparison.Ordinal);
    }
}
