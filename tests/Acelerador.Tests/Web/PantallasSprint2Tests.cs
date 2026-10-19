using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Core.Reglas;
using Acelerador.Infra.Datos;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Core;
using Acelerador.Tests.Soporte;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Acelerador.Tests.Web;

/// <summary>Recorre las pantallas del Sprint 2 como el consultor: UX-03, UX-04 (versiones), UX-05, UX-06 y el registro de UX-09.</summary>
[Collection("Web")]
public class PantallasSprint2Tests
{
    internal const string Clave = "Cl4ve-Sprint2-Única-777";

    private static readonly Dictionary<string, string?> Vacio = [];

    // ---------- Ayudas del recorrido ----------

    internal static OpcionesSimulador Simulador(FabricaWeb app) =>
        app.Services.GetRequiredService<IOptionsMonitor<OpcionesSimulador>>().CurrentValue;

    internal static async Task<string> CrearClienteAsync(Navegador nav, string nombre = "Grupo Ejemplo Andino")
    {
        await nav.GetAsync("/?nuevo=true");
        var r = await nav.PostAsync("/?handler=Crear", new Dictionary<string, string?> { ["Nombre"] = nombre }, seguir: false);
        var destino = r.Headers.Location!.OriginalString;
        await nav.GetAsync(destino);
        return destino.Replace("/conexiones", "", StringComparison.Ordinal);
    }

    internal static async Task ProbarAsync(Navegador nav, string cliente, SistemaSap s, string host, string mandante)
    {
        await nav.GetAsync($"{cliente}/conexiones");
        await nav.PostAsync($"{cliente}/conexiones?handler=Probar&sistema={s}", new Dictionary<string, string?>
        {
            ["host"] = host,
            ["mandante"] = mandante,
            ["usuarioTecnico"] = "rfc_acel",
            ["credencial"] = Clave,
        });
    }

    /// <summary>UX-01 → UX-02 → UX-03: crea el cliente, prueba y valida las conexiones y entra al diagnóstico.</summary>
    internal static async Task<string> LlegarADiagnosticoAsync(Navegador nav, string bw = "bw-ejemplo.local", string bpc = "bw-ejemplo.local")
    {
        var cliente = await CrearClienteAsync(nav);
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");
        await ProbarAsync(nav, cliente, SistemaSap.Bw, bw, "100");
        await ProbarAsync(nav, cliente, SistemaSap.Bpc, bpc, "100");
        await nav.PostAsync($"{cliente}/conexiones?handler=Validar", Vacio);
        await nav.PostAsync($"{cliente}/conexiones?handler=Continuar", Vacio);
        Assert.Equal($"{cliente}/diagnostico", nav.Ruta);
        return cliente;
    }

    internal static async Task<string> LlegarAModulosAsync(Navegador nav)
    {
        var cliente = await LlegarADiagnosticoAsync(nav);
        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Vacio);
        await nav.PostAsync($"{cliente}/diagnostico?handler=Campos", new Dictionary<string, string?> { ["accion"] = "continuar" });
        Assert.Equal($"{cliente}/modulos", nav.Ruta);
        return cliente;
    }

    internal static async Task<string> LlegarASociedadesAsync(Navegador nav, IReadOnlyList<Modulo>? modulos = null)
    {
        var cliente = await LlegarAModulosAsync(nav);
        await nav.PostAsync($"{cliente}/modulos", (modulos ?? [Modulo.Consolidacion]).Select(m => new KeyValuePair<string, string>("modulos", m.ToString()))
            .Append(new KeyValuePair<string, string>("accion", "continuar")).ToList());
        Assert.Equal($"{cliente}/sociedades", nav.Ruta);
        return cliente;
    }

    internal static Dictionary<string, string?> FormularioSociedades(IEnumerable<FilaSociedad> filas, string accion)
    {
        var d = new Dictionary<string, string?> { ["accion"] = accion };
        var i = 0;
        foreach (var f in filas)
        {
            d[$"filas[{i}].Codigo"] = f.Codigo;
            d[$"filas[{i}].Nombre"] = f.Nombre;
            d[$"filas[{i}].EnSap"] = f.EnSap ? "true" : null;
            d[$"filas[{i}].MonedaLocal"] = f.MonedaLocal;
            d[$"filas[{i}].Matriz"] = f.Matriz;
            d[$"filas[{i}].PlanCuentas"] = f.PlanCuentas;
            d[$"filas[{i}].Porcentaje"] = f.Porcentaje;
            d[$"filas[{i}].Metodo"] = f.Metodo;
            i++;
        }

        return d;
    }

    internal static async Task<string> LlegarAMapeoAsync(Navegador nav, IEnumerable<FilaSociedad>? filas = null, IReadOnlyList<Modulo>? modulos = null)
    {
        var cliente = await LlegarASociedadesAsync(nav, modulos);
        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(filas ?? Entorno.FilasPrototipo(), "continuar"));
        Assert.Equal($"{cliente}/mapeo", nav.Ruta);
        return cliente;
    }

    private static Dictionary<string, string?> FormularioVersiones(IReadOnlyList<VersionPresupuesto> versiones, string accion)
    {
        var d = new Dictionary<string, string?> { ["accion"] = accion, ["conVersiones"] = "true" };
        for (var i = 0; i < versiones.Count; i++)
        {
            d[$"versiones[{i}].Codigo"] = versiones[i].Codigo;
            d[$"versiones[{i}].Nombre"] = versiones[i].Nombre;
            d[$"versiones[{i}].Anual"] = versiones[i].Anual ? "true" : "false";
        }

        return d;
    }

    private static Task PostRepetidoAsync(Navegador nav, string accion, Dictionary<string, string?> campos, params string[] modulos) =>
        nav.PostAsync(accion, campos.Where(c => c.Value is not null).Select(c => new KeyValuePair<string, string>(c.Key, c.Value!))
            .Concat(modulos.Select(m => new KeyValuePair<string, string>("modulos", m))));

    // ---------- UX-03 Diagnóstico e inventario (HU-07) ----------

    [Fact(DisplayName = "HU-07: antes del primer escaneo UX-03 lo pide y no deja continuar")]
    [Trait("HU", "HU-07")]
    public async Task UX03SinEscaneo()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await LlegarADiagnosticoAsync(nav);

        Assert.Contains("<h1>Diagnóstico e inventario</h1>", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("Aún no se ha escaneado el sistema del cliente.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains(">Escanear</button>", nav.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("Continuar a m", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-07 CA1: el inventario se muestra en pestañas: extractores, InfoProviders (InfoCubos), modelos BPC y campos Z")]
    [Trait("HU", "HU-07")]
    public async Task UX03Pestanas()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarADiagnosticoAsync(nav);

        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Vacio);

        Assert.Contains("Escaneo completo.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("role=\"tablist\"", nav.Html, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Count(nav.Html, "role=\"tab\""));
        Assert.Equal(4, Regex.Count(nav.Html, "role=\"tabpanel\""));
        foreach (var t in new[] { "Extractores activos (4)", "InfoProviders (InfoCubos) (2)", "Modelos BPC (1)", "Campos Z (3)" })
        {
            Assert.Contains(t, nav.Texto, StringComparison.Ordinal);
        }

        Assert.Contains("0FI_GL_14", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("ZSD_C01", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("PLAN_VENTAS", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Inactivo", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"pnl-CamposZ\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Incorporar ZZREGION\"", nav.Html, StringComparison.Ordinal);

        var js = await (await nav.Http.GetAsync("/js/acelerador.js")).Content.ReadAsStringAsync();
        Assert.Contains("ArrowRight", js, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-07 CA1: con BW/4HANA la pestaña dice InfoProviders (aDSO)")]
    [Trait("HU", "HU-07")]
    public async Task UX03Adso()
    {
        using var app = new FabricaWeb(PerfilSimulado.ABw4Hana);
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarADiagnosticoAsync(nav);

        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Vacio);

        Assert.Contains("InfoProviders (aDSO) (2)", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("ZSD_D01", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-07 CA2: si BW ya tiene la bodega se marca «Bodega existente: Sí» y no se recrea (RN-03)")]
    [Trait("HU", "HU-07")]
    public async Task UX03Bodega()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarADiagnosticoAsync(nav, bw: "bw-bodega.local");

        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Vacio);

        Assert.Contains("Bodega existente: Sí", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Se usará la bodega existente; no se recrea. RN-03", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("ZACC_C01", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-07 CA2 y estado vacío: sin bodega ni objetos cada pestaña tiene su mensaje")]
    [Trait("HU", "HU-07")]
    public async Task UX03Vacio()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarADiagnosticoAsync(nav);
        Simulador(app).InventarioVacio = true;

        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Vacio);

        Assert.Contains("Bodega existente: No", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("El cliente no tiene bodega en BW: se generará completa.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("El cliente no tiene bodega en BW. Se generará completa en el plan de generación.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("No se encontraron campos Z. El cliente no ha creado campos adicionales en SAP.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("No hay modelos BPC.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("No se encontraron extractores.", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-07 CA3: Volver a escanear repite el inventario y actualiza la fecha del último escaneo")]
    [Trait("HU", "HU-07")]
    public async Task UX03VolverAEscanear()
    {
        var reloj = new RelojFijo(new DateTimeOffset(2026, 10, 16, 15, 0, 0, TimeSpan.Zero));
        using var app = new FabricaWeb(PerfilSimulado.ABw75, s => s.Replace(ServiceDescriptor.Singleton<TimeProvider>(reloj)));
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarADiagnosticoAsync(nav);
        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Vacio);
        var antes = Presentacion(reloj.Ahora);
        Assert.Contains($"Último escaneo: {antes}", nav.Texto, StringComparison.Ordinal);

        reloj.Ahora = reloj.Ahora.AddDays(1).AddMinutes(7);
        Simulador(app).CamposZAdicionales.Add("ZZNUEVO");
        Assert.Contains(">Volver a escanear</button>", nav.Html, StringComparison.Ordinal);
        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Vacio);

        Assert.Contains($"Último escaneo: {Presentacion(reloj.Ahora)}", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("ZZNUEVO", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Campos Z (4)", nav.Texto, StringComparison.Ordinal);
    }

    private static string Presentacion(DateTimeOffset f) => Acelerador.Web.Formato.Fecha(f);

    [Fact(DisplayName = "HU-07 CA4: si el escaneo falla se muestra el paso que falló, el mensaje del sistema y Reintentar")]
    [Trait("HU", "HU-07")]
    public async Task UX03Error()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarADiagnosticoAsync(nav, bpc: "bw-sinpermiso.local");

        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Vacio);

        Assert.Contains("role=\"alert\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("El escaneo no terminó.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Falló el paso 3 de 4: Modelos BPC (SAP BPC)", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"«{SimulatedSapGateway.MensajeFalloInventario(TipoInventario.ModelosBpc)}»", nav.Texto, StringComparison.Ordinal);
        Assert.Contains(">Reintentar</button>", nav.Html, StringComparison.Ordinal);
        Assert.DoesNotContain(Clave, nav.Html, StringComparison.Ordinal);

        // Aunque alguien envíe Continuar, el servidor no avanza sin un escaneo completo.
        await nav.PostAsync($"{cliente}/diagnostico?handler=Campos", new Dictionary<string, string?> { ["accion"] = "continuar" });
        Assert.Equal($"{cliente}/diagnostico", nav.Ruta);
    }

    [Fact(DisplayName = "HU-07 estado de carga: Escanear muestra la barra «Escaneando BW y BPC…» mientras SAP responde")]
    [Trait("HU", "HU-07")]
    public async Task UX03Carga()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await LlegarADiagnosticoAsync(nav);

        Assert.Contains("Escaneando BW y BPC…", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("data-carga-tipo=\"barra\"", nav.Html, StringComparison.Ordinal);

        var js = await (await nav.Http.GetAsync("/js/acelerador.js")).Content.ReadAsStringAsync();
        Assert.Contains("function mostrarCarga", js, StringComparison.Ordinal);
        Assert.Contains("class=\"barra\"", js, StringComparison.Ordinal);
        var css = await (await nav.Http.GetAsync("/css/acelerador.css")).Content.ReadAsStringAsync();
        Assert.Contains(".barra span", css, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "RF-09: los campos Z marcados se guardan y siguen marcados al volver")]
    [Trait("HU", "HU-07")]
    public async Task UX03CamposZ()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarADiagnosticoAsync(nav);
        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", Vacio);

        await nav.PostAsync($"{cliente}/diagnostico?handler=Campos", new Dictionary<string, string?> { ["campos"] = "ZZLINEA", ["accion"] = "guardar" });

        Assert.Contains("Selección de campos Z guardada.", nav.Texto, StringComparison.Ordinal);
        Assert.Matches("value=\"ZZLINEA\" id=\"cz-ZZLINEA\" checked=\"checked\"", nav.Html);
        Assert.DoesNotMatch("value=\"ZZREGION\" id=\"cz-ZZREGION\" checked", nav.Html);
    }

    // ---------- UX-04 Versiones de presupuesto (HU-12) ----------

    [Fact(DisplayName = "HU-12 CA1: con Presupuesto marcado se muestran las versiones propuestas; sin él la sección queda oculta")]
    [Trait("HU", "HU-12")]
    public async Task UX04VersionesVisibles()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAModulosAsync(nav);

        Assert.Contains("data-versiones hidden=\"hidden\"", nav.Html, StringComparison.Ordinal);

        await nav.PostAsync($"{cliente}/modulos", new Dictionary<string, string?> { ["modulos"] = "Presupuesto", ["accion"] = "guardar" });

        Assert.DoesNotContain("data-versiones hidden", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Versiones de presupuesto", nav.Texto, StringComparison.Ordinal);
        foreach (var v in VersionPresupuesto.Propuestas)
        {
            Assert.Contains($"value=\"{v.Codigo}\"", nav.Html, StringComparison.Ordinal);
        }

        var js = await (await nav.Http.GetAsync("/js/acelerador.js")).Content.ReadAsStringAsync();
        Assert.Contains("seccion.hidden = !presupuesto.checked", js, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-12 CA1: se agregan, renombran y quitan versiones, y se guardan")]
    [Trait("HU", "HU-12")]
    public async Task UX04EditarVersiones()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAModulosAsync(nav);
        await nav.PostAsync($"{cliente}/modulos", new Dictionary<string, string?> { ["modulos"] = "Presupuesto", ["accion"] = "guardar" });
        var propuestas = VersionPresupuesto.Propuestas.ToList();

        await PostRepetidoAsync(nav, $"{cliente}/modulos", FormularioVersiones(propuestas, "agregar-version"), "Presupuesto");
        Assert.Contains("name=\"versiones[4].Codigo\" value=\"\"", nav.Html, StringComparison.Ordinal);

        var editadas = propuestas.Append(new VersionPresupuesto("V2", "Versión 2")).ToList();
        await PostRepetidoAsync(nav, $"{cliente}/modulos", FormularioVersiones(editadas, "quitar-version-3"), "Presupuesto");
        Assert.DoesNotContain("value=\"PROY_9_3\"", nav.Html, StringComparison.Ordinal);

        editadas = [propuestas[0], propuestas[1] with { Nombre = "Reproyección 3+9" }, propuestas[2], new("V2", "Versión 2")];
        await PostRepetidoAsync(nav, $"{cliente}/modulos", FormularioVersiones(editadas, "guardar"), "Presupuesto");

        Assert.Contains("Selección guardada.", nav.Texto, StringComparison.Ordinal);
        var guardado = (await app.Services.GetRequiredService<IClienteRepository>().ObtenerAsync(Guid.Parse(cliente.Split('/')[^1])))!;
        Assert.Equal(["PRESUP_ANUAL", "PROY_3_9", "PROY_6_6", "V2"], guardado.Versiones.Select(v => v.Codigo));
        Assert.Equal("Reproyección 3+9", guardado.Versiones[1].Nombre);
    }

    [Fact(DisplayName = "HU-12 CA2: quitar la versión de presupuesto anual y guardar muestra el error y no guarda")]
    [Trait("HU", "HU-12")]
    public async Task UX04SinAnual()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAModulosAsync(nav);
        await nav.PostAsync($"{cliente}/modulos", new Dictionary<string, string?> { ["modulos"] = "Presupuesto", ["accion"] = "guardar" });

        await PostRepetidoAsync(nav, $"{cliente}/modulos", FormularioVersiones(VersionPresupuesto.Propuestas.Skip(1).ToList(), "continuar"), "Presupuesto");

        Assert.Equal($"{cliente}/modulos", nav.Ruta);
        Assert.Contains("role=\"alert\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Debe existir la versión de presupuesto anual.", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-12 CA3: un código de versión repetido se marca en la fila y no se guarda")]
    [Trait("HU", "HU-12")]
    public async Task UX04CodigoRepetido()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAModulosAsync(nav);
        await nav.PostAsync($"{cliente}/modulos", new Dictionary<string, string?> { ["modulos"] = "Presupuesto", ["accion"] = "guardar" });

        var versiones = VersionPresupuesto.Propuestas.Append(new VersionPresupuesto("proy_6_6", "Otra")).ToList();
        await PostRepetidoAsync(nav, $"{cliente}/modulos", FormularioVersiones(versiones, "guardar"), "Presupuesto");

        Assert.Contains("El código de versión PROY_6_6 está repetido.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("id=\"ver-cod-4\" name=\"versiones[4].Codigo\" value=\"proy_6_6\"", nav.Html, StringComparison.Ordinal);
        Assert.Matches("ver-cod-4[^>]+aria-invalid=\"true\"", nav.Html);
    }

    // ---------- UX-05 Sociedades del grupo (HU-09, HU-10) ----------

    [Fact(DisplayName = "HU-09 estado vacío: «Agrega las sociedades del grupo que entran en la consolidación.»")]
    [Trait("HU", "HU-09")]
    public async Task UX05Vacio()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await LlegarASociedadesAsync(nav);

        Assert.Contains("Agrega las sociedades del grupo que entran en la consolidación.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Clientes y proveedores no son entidades de consolidación: solo las sociedades del grupo. RN-07", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("La consolidación se presenta en moneda local y en dólares. RN-06", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("value=\"continuar\" disabled=\"disabled\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-09 CA1 y CA2: Agregar sociedad ofrece solo las del ERP (más «no SAP») y trae sus datos; se edita y se guarda")]
    [Trait("HU", "HU-09")]
    public async Task UX05AgregarYEditar()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);

        Assert.Contains("<option value=\"1000\">", nav.Html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"no-sap\">", nav.Html, StringComparison.Ordinal);
        Assert.Equal(4, Regex.Count(nav.Html, "<option value=\"(1000|2000|3000|no-sap)\">"));

        await nav.PostAsync($"{cliente}/sociedades", new Dictionary<string, string?> { ["nueva"] = "1000", ["accion"] = "agregar" });
        Assert.Contains("name=\"filas[0].Codigo\" value=\"1000\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("name=\"filas[0].MonedaLocal\" value=\"COP\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("name=\"filas[0].PlanCuentas\" value=\"PCCO\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("<option value=\"Matriz\" selected=\"selected\">Matriz</option>", nav.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"1000\">1000", nav.Html, StringComparison.Ordinal);

        var filas = Entorno.FilasPrototipo();
        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(filas, "guardar"));
        Assert.Contains("Sociedades guardadas.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("4 sociedades · 1 no SAP", nav.Texto, StringComparison.Ordinal);

        filas[2] = filas[2] with { Nombre = "Filial Panamá S.A." };
        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(filas, "guardar"));
        await nav.GetAsync($"{cliente}/sociedades");
        Assert.Contains("value=\"Filial Panamá S.A.\"", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("name=\"filas[3].Porcentaje\" value=\"35\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-09 CA2: un código que no es sociedad del grupo en el ERP se rechaza (RN-07)")]
    [Trait("HU", "HU-09")]
    public async Task UX05SoloGrupo()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);
        var filas = Entorno.FilasPrototipo();
        filas.Add(new("T901", "Proveedor de prueba", true, "COP", "1000", "PCCO", "0", "FueraDelPerimetro"));

        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(filas, "guardar"));

        Assert.Contains("T901 no es una sociedad del grupo en el ERP. Clientes y proveedores no son entidades de consolidación (RN-07).", nav.Texto, StringComparison.Ordinal);
        Assert.Matches("id=\"soc-4-codigo\"[^>]+aria-invalid=\"true\"", nav.Html);
        await nav.GetAsync($"{cliente}/sociedades");
        Assert.Contains("Agrega las sociedades del grupo", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-09 CA3 y CA4: cada sociedad muestra su plan de cuentas; sin moneda local se avisa y no se guarda")]
    [Trait("HU", "HU-09")]
    public async Task UX05SinMoneda()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);
        var filas = Entorno.FilasPrototipo();
        filas.Add(new("D500", "Filial D500", false, "", "1000", "", "100", "IntegracionGlobal"));

        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(filas, "guardar"));

        Assert.Contains("La sociedad D500 no tiene moneda local.", nav.Texto, StringComparison.Ordinal);
        Assert.Matches("id=\"soc-4-moneda\"[^>]+aria-invalid=\"true\"", nav.Html);
        Assert.Contains("name=\"filas[2].PlanCuentas\" value=\"PCPA\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("RN-05", nav.Texto, StringComparison.Ordinal);
        await nav.GetAsync($"{cliente}/sociedades");
        Assert.Contains("Agrega las sociedades del grupo", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-09 CA5: una sociedad no SAP habilita el paso «Sociedades no SAP» (UX-07) en la navegación")]
    [Trait("HU", "HU-09")]
    public async Task UX05NoSapHabilitaUx07()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);

        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(Entorno.FilasPrototipo().Take(3), "guardar"));
        Assert.DoesNotMatch(@"\d\. Sociedades no SAP", nav.Texto);
        Assert.Contains("5. Mapeo de cuentas", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("6. Plan de generación", nav.Texto, StringComparison.Ordinal);

        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(Entorno.FilasPrototipo(), "guardar"));
        Assert.Contains("6. Sociedades no SAP", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("7. Plan de generación", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-10 CA1: cada fila tiene % de propiedad y el método con las cuatro opciones de RN-13")]
    [Trait("HU", "HU-10")]
    public async Task UX05PorcentajeYMetodo()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);

        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(Entorno.FilasPrototipo(), "guardar"));

        Assert.Contains("<th scope=\"col\" class=\"num\">% propiedad</th>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Método inicial", nav.Texto, StringComparison.Ordinal);
        foreach (var m in new[] { "Matriz", "Integración global", "Participación (coligada)", "Fuera del perímetro" })
        {
            Assert.Contains(m, nav.Texto, StringComparison.Ordinal);
        }

        Assert.Contains("inputmode=\"decimal\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("RN-13", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-10 CA2: dos sociedades con método Matriz se marcan y no se guarda")]
    [Trait("HU", "HU-10")]
    public async Task UX05DosMatrices()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);
        var filas = Entorno.FilasPrototipo();
        filas[1] = filas[1] with { Metodo = "Matriz", Matriz = "" };

        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(filas, "guardar"));

        Assert.Contains("Solo una sociedad puede tener el método Matriz: 1000 y 2000 lo tienen.", nav.Texto, StringComparison.Ordinal);
        Assert.Matches("id=\"soc-0-metodo\"[^>]+aria-invalid=\"true\"", nav.Html);
        Assert.Matches("id=\"soc-1-metodo\"[^>]+aria-invalid=\"true\"", nav.Html);
    }

    [Theory(DisplayName = "HU-10 CA3: un porcentaje fuera de 0 a 100 se marca en la fila y no se guarda")]
    [Trait("HU", "HU-10")]
    [InlineData("120")]
    [InlineData("−0,01")]
    [InlineData("100,01")]
    public async Task UX05PorcentajeFueraDeRango(string porcentaje)
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);
        var filas = Entorno.FilasPrototipo();
        filas[1] = filas[1] with { Porcentaje = porcentaje };

        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(filas, "guardar"));

        Assert.Contains("El % de propiedad de 2000 debe estar entre 0 y 100.", nav.Texto, StringComparison.Ordinal);
        Assert.Matches("id=\"soc-1-porcentaje\"[^>]+aria-invalid=\"true\"", nav.Html);
        Assert.Contains("<tr class=\"fila-err\">", nav.Html, StringComparison.Ordinal);
        Assert.Contains("id=\"soc-1-err\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("aria-describedby=\"soc-1-err\"", nav.Html, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "HU-10 CA3: 0 y 100 se guardan y se ven igual al recargar")]
    [Trait("HU", "HU-10")]
    [InlineData("0")]
    [InlineData("100")]
    public async Task UX05PorcentajeLimite(string porcentaje)
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);
        var filas = Entorno.FilasPrototipo();
        filas[3] = filas[3] with { Porcentaje = porcentaje };

        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(filas, "guardar"));
        await nav.GetAsync($"{cliente}/sociedades");

        Assert.Contains($"name=\"filas[3].Porcentaje\" value=\"{porcentaje}\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-09 estado de error: si la base local falla se muestra «No se pudieron guardar las sociedades» con la causa")]
    [Trait("HU", "HU-09")]
    public async Task UX05ErrorAlGuardar()
    {
        using var app = new FabricaWeb(PerfilSimulado.ABw75, s => s.Replace(ServiceDescriptor.Singleton<ISociedadRepository, ServicioSociedadesTests.SociedadesRotas>()));
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);

        await nav.PostAsync($"{cliente}/sociedades", FormularioSociedades(Entorno.FilasPrototipo(), "guardar"));

        Assert.Contains("No se pudieron guardar las sociedades.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("disk I/O error", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("name=\"filas[3].Codigo\" value=\"9000\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-09 estado de error: si el ERP no responde se muestra el mensaje y Reintentar")]
    [Trait("HU", "HU-09")]
    public async Task UX05ErrorErp()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);
        Simulador(app).FallaSociedades = true;

        await nav.GetAsync($"{cliente}/sociedades");

        Assert.Contains("No se pudieron leer las sociedades del ERP.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains(SimulatedSapGateway.MensajeFalloSociedades, nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Reintentar", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-09 estado de carga: abrir Sociedades muestra «Cargando sociedades…» mientras se lee el ERP")]
    [Trait("HU", "HU-09")]
    public async Task UX05Carga()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarASociedadesAsync(nav);

        var html = WebUtility.HtmlDecode(nav.Html);
        Assert.Contains($"href=\"{cliente}/sociedades\" aria-current=\"step\" data-carga=\"Cargando sociedades…\"", html, StringComparison.Ordinal);
        Assert.Contains("data-carga=\"Cargando cuentas…\">Continuar al mapeo", html, StringComparison.Ordinal);
    }

    // ---------- UX-06 Mapeo de cuentas (HU-11) ----------

    [Fact(DisplayName = "HU-11 estado vacío y CA3: sin mapeos se muestra «Aún no hay cuentas mapeadas.» y el contador de sin mapear")]
    [Trait("HU", "HU-11")]
    public async Task UX06Vacio()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await LlegarAMapeoAsync(nav);

        var total = SimulatedSapGateway.Cuentas.Count(c => c.Plan == "PCCO");
        Assert.Contains("Aún no hay cuentas mapeadas.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"{total} cuentas sin mapear", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("PCCO (de la sociedad)", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains("Cuentas filtradas por el plan de cuentas de la sociedad (PCCO). RN-05", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("11050501", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("<option value=\"9000\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-11 CA1, CA2 y CA3: se eligen rubro y cuentas, se asignan, la vista previa suma y el contador baja")]
    [Trait("HU", "HU-11")]
    public async Task UX06Asignar()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAMapeoAsync(nav);
        var total = SimulatedSapGateway.Cuentas.Count(c => c.Plan == "PCCO");

        await nav.GetAsync($"{cliente}/mapeo?sociedad=1000&rubro=F-00-101");
        Assert.Contains("aria-current=\"true\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Asignar al rubro F-00-101", nav.Texto, StringComparison.Ordinal);

        await nav.PostAsync($"{cliente}/mapeo",
        [
            new("sociedad", "1000"), new("rubro", "F-00-101"), new("sinMapear", "false"),
            new("cuentas", "11050501"), new("cuentas", "11100501"), new("accion", "asignar"),
        ]);
        Assert.StartsWith($"{cliente}/mapeo?sociedad=1000&rubro=F-00-101", nav.Ruta, StringComparison.Ordinal);

        Assert.Contains("2 cuentas asignadas a F-00-101.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("suma de 2 cuentas = COP 12.632.300.000 (saldos a 2026-09)", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("El valor del rubro es la suma de sus cuentas. RN-10", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"{total - 2} cuentas sin mapear", nav.Texto, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Count(nav.Html, "<tr class=\"resaltada\">"));
        Assert.Contains("2 ctas", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Aún no hay cuentas mapeadas.", nav.Texto, StringComparison.Ordinal);

        // CA3: «Solo sin mapear» deja fuera las asignadas.
        await nav.GetAsync($"{cliente}/mapeo?sociedad=1000&rubro=F-00-101&sinMapear=true");
        Assert.DoesNotContain(">11050501<", nav.Html, StringComparison.Ordinal);
        Assert.Contains(">11200501<", nav.Html, StringComparison.Ordinal);
        Assert.Contains("checked=\"checked\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-11 CA1 y RN-05: al cambiar de sociedad se listan las cuentas de su plan")]
    [Trait("HU", "HU-11")]
    public async Task UX06OtraSociedad()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAMapeoAsync(nav);

        await nav.GetAsync($"{cliente}/mapeo?sociedad=3000");

        Assert.Contains("PCPA (de la sociedad)", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains(">1010<", nav.Html, StringComparison.Ordinal);
        Assert.DoesNotContain(">11050501<", nav.Html, StringComparison.Ordinal);
        Assert.Contains("USD 0", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-11 CA4: si el ERP no responde se muestra el mensaje del sistema y Reintentar")]
    [Trait("HU", "HU-11")]
    public async Task UX06ErrorErp()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAMapeoAsync(nav);
        Simulador(app).FallaCuentas = true;

        await nav.GetAsync($"{cliente}/mapeo");

        Assert.Contains("No se pudieron leer las cuentas del ERP.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains(SimulatedSapGateway.MensajeFalloCuentas, nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Reintentar", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-11 CA5: si el guardado falla se informa cuántas asignaciones no se guardaron")]
    [Trait("HU", "HU-11")]
    public async Task UX06ErrorAlGuardar()
    {
        using var app = new FabricaWeb(PerfilSimulado.ABw75, s => s.Replace(ServiceDescriptor.Singleton<IMapeoRepository, MapeoRoto>()));
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAMapeoAsync(nav);

        var r = await nav.PostAsync($"{cliente}/mapeo",
        [
            new("sociedad", "1000"), new("rubro", "F-00-101"), new("cuentas", "11050501"), new("cuentas", "11100501"),
            new("cuentas", "11200501"), new("accion", "asignar"),
        ]);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("No se pudo guardar el mapeo.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("La asignación de 3 cuentas no se guardó. Vuelve a asignarlas.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-11 estado de carga: abrir el mapeo y cambiar de filtro muestran «Cargando cuentas…»")]
    [Trait("HU", "HU-11")]
    public async Task UX06Carga()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await LlegarAMapeoAsync(nav);

        var html = WebUtility.HtmlDecode(nav.Html);
        Assert.Contains("aria-current=\"step\" data-carga=\"Cargando cuentas…\"", html, StringComparison.Ordinal);
        Assert.Contains("data-carga=\"Cargando cuentas…\" data-auto", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-09 CA5 y flujo: Continuar en el mapeo lleva a UX-07 si hay sociedades no SAP")]
    [Trait("HU", "HU-11")]
    public async Task UX06Continuar()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAMapeoAsync(nav);

        await nav.PostAsync($"{cliente}/mapeo?handler=Continuar", Vacio);

        Assert.Equal($"{cliente}/no-sap", nav.Ruta);
        Assert.Contains("Carga de sociedades no SAP", nav.Texto, StringComparison.Ordinal);
    }

    private sealed class MapeoRoto : IMapeoRepository
    {
        public Task<IReadOnlyList<AsignacionCuenta>> ListarAsync(Guid clienteId, string planCuentas, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AsignacionCuenta>>([]);

        public Task GuardarAsync(Guid clienteId, string planCuentas, IReadOnlyList<string> cuentas, string? rubro, CancellationToken ct = default) =>
            throw new SqliteException("database is locked", 5);
    }

    // ---------- UX-09 Registro de ejecuciones (HU-54) ----------

    [Fact(DisplayName = "HU-54 CA1: el registro muestra fecha y hora, usuario, sistema, paso, objetos y resultado")]
    [Trait("HU", "HU-54")]
    public async Task UX09Registro()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAModulosAsync(nav);

        await nav.GetAsync($"{cliente}/registro");

        Assert.Contains("<h1>Registro de ejecuciones</h1>", nav.Html, StringComparison.Ordinal);
        foreach (var c in new[] { "Fecha y hora", "Usuario", "Sistema", "Paso", "Objetos", "Resultado" })
        {
            Assert.Contains($"<th scope=\"col\">{WebUtility.HtmlEncode(c)}</th>", nav.Html.Replace("&#xF3;", "ó", StringComparison.Ordinal), StringComparison.Ordinal);
        }

        Assert.Contains("Inventario: Modelos BPC", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Probar conexión", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Ninguno (solo lectura)", nav.Texto, StringComparison.Ordinal);
        Assert.Contains(Environment.UserName, nav.Texto, StringComparison.Ordinal);
        Assert.Contains("9 de 9 ejecuciones", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"page\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("RNF-Aud-01", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-54: el registro se filtra por resultado")]
    [Trait("HU", "HU-54")]
    public async Task UX09Filtro()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-falla.local", "300");
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");

        await nav.GetAsync($"{cliente}/registro?resultado=Fallido");

        Assert.Contains("1 de 2 ejecuciones", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("not reached", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("<option value=\"Fallido\" selected=\"selected\">Fallidos</option>", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-54 CA3: Descargar registro entrega un CSV con los mismos campos")]
    [Trait("HU", "HU-54")]
    public async Task UX09Descargar()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");
        await nav.GetAsync($"{cliente}/registro");
        Assert.Contains($"href=\"{cliente}/registro?handler=Descargar\" download", nav.Html, StringComparison.Ordinal);

        var r = await nav.Http.GetAsync($"{cliente}/registro?handler=Descargar");
        var csv = Encoding.UTF8.GetString(await r.Content.ReadAsByteArrayAsync()).TrimStart('﻿');

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("text/csv", r.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("registro-ejecuciones-grupo-ejemplo-andino-", r.Content.Headers.ContentDisposition!.FileName!.Trim('"'), StringComparison.Ordinal);
        Assert.StartsWith("\"Fecha y hora\";\"Usuario\";\"Sistema\";\"Paso\";\"Objetos\";\"Resultado\";\"Detalle\"\r\n", csv, StringComparison.Ordinal);
        Assert.Contains("\"SAP ERP\";\"Probar conexión\";\"Ninguno (solo lectura)\";\"Exitoso\"", csv, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-54 CA4: ni la pantalla del registro ni su descarga contienen la credencial")]
    [Trait("HU", "HU-54")]
    public async Task UX09SinCredencial()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAMapeoAsync(nav);
        Simulador(app).EcoDeCredencialEnErrores = true;
        await ProbarAsync(nav, cliente, SistemaSap.Bpc, "bw-ejemplo.local", "100");

        await nav.GetAsync($"{cliente}/registro");
        var csv = await (await nav.Http.GetAsync($"{cliente}/registro?handler=Descargar")).Content.ReadAsStringAsync();

        Assert.Contains("Fallido", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(Clave, nav.Html, StringComparison.Ordinal);
        Assert.DoesNotContain(Clave, csv, StringComparison.Ordinal);
        Assert.DoesNotContain(app.Registro.Lineas, l => l.Contains(Clave, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "HU-54 estado vacío: «Aún no hay ejecuciones.»")]
    [Trait("HU", "HU-54")]
    public async Task UX09Vacio()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);

        await nav.GetAsync($"{cliente}/registro");

        Assert.Contains("Aún no hay ejecuciones.", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Descargar registro", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-54 estado de error: si la base no responde se muestra «No se pudo cargar el registro.»")]
    [Trait("HU", "HU-54")]
    public async Task UX09Error()
    {
        using var app = new FabricaWeb(PerfilSimulado.ABw75, s => s.Replace(ServiceDescriptor.Singleton<IRegistroEjecuciones, RegistroRoto>()));
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);

        await nav.GetAsync($"{cliente}/registro");

        Assert.Contains("No se pudo cargar el registro.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-54 CA2: al arrancar, la aplicación depura solo las entradas de más de 12 meses")]
    [Trait("HU", "HU-54")]
    public async Task UX09DepuraAlArrancar()
    {
        using var app = new FabricaWeb();
        var db = new BaseLocal(app.RutaBase);
        await db.InicializarAsync();
        var registro = new SqliteRegistroEjecuciones(db);
        var c = Guid.NewGuid();
        var ahora = DateTimeOffset.UtcNow;
        await registro.RegistrarAsync(new Ejecucion(ahora.AddMonths(-12).AddDays(-1), "u", c, "SAP ERP", "Viejo", "—", ResultadoEjecucion.Exitoso, "viejo"));
        await registro.RegistrarAsync(new Ejecucion(ahora.AddMonths(-11), "u", c, "SAP ERP", "Reciente", "—", ResultadoEjecucion.Exitoso, "reciente"));
        SqliteConnection.ClearAllPools();

        await app.NavegadorAsync();

        Assert.Equal(["Reciente"], (await registro.ListarAsync(c)).Select(e => e.Paso));
    }

    private sealed class RegistroRoto : IRegistroEjecuciones
    {
        public Task RegistrarAsync(Ejecucion ejecucion, CancellationToken ct = default) => Task.CompletedTask;

        public Task<IReadOnlyList<Ejecucion>> ListarAsync(Guid? clienteId = null, CancellationToken ct = default) =>
            throw new SqliteException("disk I/O error", 10);
    }

    // ---------- Recorrido completo y accesibilidad ----------

    [Fact(DisplayName = "Flujo: UX-01 → UX-02 → UX-03 → UX-04 → UX-05 → UX-06 con el simulador")]
    public async Task RecorridoCompleto()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();

        var cliente = await LlegarAMapeoAsync(nav);

        Assert.Contains("<h1>Mapeo de cuentas</h1>", nav.Html, StringComparison.Ordinal);
        foreach (var paso in new[] { "1. Conexiones", "2. Diagnóstico", "3. Módulos", "4. Sociedades" })
        {
            Assert.Matches($"{Regex.Escape(WebUtility.HtmlEncode(paso).Replace("&#xF3;", "ó", StringComparison.Ordinal))}|{Regex.Escape(paso)}", WebUtility.HtmlDecode(nav.Html));
        }

        Assert.Equal(4, Regex.Count(nav.Texto, "Completo"));
        await nav.GetAsync("/");
        Assert.Contains(">Mapeo de cuentas</td>", WebUtility.HtmlDecode(nav.Html), StringComparison.Ordinal);
        Assert.Contains($"href=\"{cliente}/mapeo\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "WCAG 2.1 AA básico en UX-03, UX-04, UX-05, UX-06 y el registro: cada control tiene nombre, tablas con título y errores anunciados")]
    public async Task Accesibilidad()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAMapeoAsync(nav);
        await nav.PostAsync($"{cliente}/modulos", new Dictionary<string, string?> { ["modulos"] = "Presupuesto", ["accion"] = "guardar" });

        foreach (var ruta in new[] { "diagnostico", "modulos", "sociedades", "mapeo", "registro" })
        {
            await nav.GetAsync($"{cliente}/{ruta}");
            var html = nav.Html;
            Assert.Contains("<html lang=\"es\">", html, StringComparison.Ordinal);
            Assert.Contains("<main class=\"a-main\" id=\"contenido\">", html, StringComparison.Ordinal);
            Assert.Single(Regex.Matches(html, "<h1>"));
            Assert.DoesNotContain(" style=\"", html, StringComparison.Ordinal);

            foreach (Match m in Regex.Matches(html, "<(input|select)\\b[^>]*>"))
            {
                var control = m.Value;
                if (control.Contains("type=\"hidden\"", StringComparison.Ordinal))
                {
                    continue;
                }

                var id = Regex.Match(control, "id=\"([^\"]+)\"").Groups[1].Value;
                var conNombre = control.Contains("aria-label=", StringComparison.Ordinal)
                    || (id.Length > 0 && html.Contains($"for=\"{id}\"", StringComparison.Ordinal));
                Assert.True(conNombre, $"{ruta}: control sin nombre accesible: {control}");
            }

            foreach (Match t in Regex.Matches(html, "<table[^>]*>\\s*<caption"))
            {
                Assert.NotEmpty(t.Value);
            }

            Assert.Equal(Regex.Count(html, "<table"), Regex.Count(html, "<caption"));
        }
    }
}
