using System.Net;
using Acelerador.Core.Dominio;
using Acelerador.Core.Puertos;
using Acelerador.Infra.Datos;
using Acelerador.Sap;
using Acelerador.Sap.Simulador;
using Acelerador.Tests.Soporte;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Acelerador.Tests.Web;

/// <summary>Recorre las pantallas como el consultor: UX-01 → UX-02 → UX-04, con el simulador SAP.</summary>
[Collection("Web")]
public class PantallasTests
{
    private const string Clave = "Cl4ve-Web-Única-555";

    private static Dictionary<string, string?> Formulario(string host, string mandante, string? clave = Clave) => new()
    {
        ["host"] = host,
        ["mandante"] = mandante,
        ["usuarioTecnico"] = "rfc_acel",
        ["credencial"] = clave,
    };

    private static async Task<string> CrearClienteAsync(Navegador nav, string nombre = "Grupo Ejemplo Andino")
    {
        await nav.GetAsync("/?nuevo=true");
        var r = await nav.PostAsync("/?handler=Crear", new Dictionary<string, string?> { ["Nombre"] = nombre }, seguir: false);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        var destino = r.Headers.Location!.OriginalString;
        await nav.GetAsync(destino);
        return destino.Replace("/conexiones", "", StringComparison.Ordinal);
    }

    private static async Task ProbarAsync(Navegador nav, string cliente, SistemaSap s, string host, string mandante, string? clave = Clave)
    {
        await nav.GetAsync($"{cliente}/conexiones");
        await nav.PostAsync($"{cliente}/conexiones?handler=Probar&sistema={s}", Formulario(host, mandante, clave));
    }

    private static async Task ConectarArquitecturaAAsync(Navegador nav, string cliente)
    {
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");
        await ProbarAsync(nav, cliente, SistemaSap.Bw, "bw-ejemplo.local", "100");
        await ProbarAsync(nav, cliente, SistemaSap.Bpc, "bw-ejemplo.local", "100");
    }

    // ---------- UX-01 Clientes (HU-02) ----------

    [Fact(DisplayName = "HU-02 CA4: sin clientes se muestra «Aún no hay clientes. Crea el primero para empezar.»")]
    [Trait("HU", "HU-02")]
    public async Task UX01Vacio()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();

        await nav.GetAsync("/");

        Assert.Contains("Aún no hay clientes. Crea el primero para empezar.", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-02 CA2 y CA3: Nuevo cliente pide el nombre y al crearlo lleva a UX-02 Conexiones")]
    [Trait("HU", "HU-02")]
    public async Task UX01CrearLlevaAConexiones()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();

        await nav.GetAsync("/?nuevo=true");
        Assert.Contains("Nombre del cliente", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("<label for=\"nombre\">", nav.Html, StringComparison.Ordinal);

        await CrearClienteAsync(nav);

        Assert.Contains("<h1>Conexiones</h1>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Grupo Ejemplo Andino", nav.Html, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "HU-02 CA2: el nombre es obligatorio y no puede repetir uno existente")]
    [Trait("HU", "HU-02")]
    [InlineData("", "El nombre del cliente es obligatorio.")]
    [InlineData("grupo ejemplo ANDINO", "Ya existe un cliente con el nombre «grupo ejemplo ANDINO».")]
    public async Task UX01NombreInvalido(string nombre, string error)
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await CrearClienteAsync(nav);

        await nav.GetAsync("/?nuevo=true");
        var r = await nav.PostAsync("/?handler=Crear", new Dictionary<string, string?> { ["Nombre"] = nombre });

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains(error, nav.Texto, StringComparison.Ordinal);
        Assert.Contains("aria-invalid=\"true\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-02 CA1, HU-06 CA4: UX-01 lista cliente, arquitectura, paso actual, estado, última ejecución y usuario")]
    [Trait("HU", "HU-02")]
    public async Task UX01Lista()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ConectarArquitecturaAAsync(nav, cliente);

        await nav.GetAsync("/");

        foreach (var columna in new[] { "Cliente", "Arquitectura", "Paso actual", "Estado", "Última ejecución", "Usuario" })
        {
            Assert.Contains($"<th scope=\"col\">{columna}</th>", nav.Html, StringComparison.Ordinal);
        }

        Assert.Contains("Grupo Ejemplo Andino", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("A · BW 7.5 + BPC 10.1 modelo estándar", nav.Texto, StringComparison.Ordinal);
        Assert.Contains(">Conexiones</td>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("En curso", nav.Texto, StringComparison.Ordinal);
        Assert.Contains(Environment.UserName, nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("<td data-l=\"Última ejecución\" class=\"mono\">—</td>", nav.Html, StringComparison.Ordinal);
        Assert.Contains($"href=\"{cliente}/conexiones\"", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-02 CA5: la búsqueda filtra la lista por nombre")]
    [Trait("HU", "HU-02")]
    public async Task UX01Busqueda()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await CrearClienteAsync(nav);
        await CrearClienteAsync(nav, "Holding Ejemplo Caribe");

        await nav.GetAsync("/?q=caribe");
        Assert.Contains("Holding Ejemplo Caribe", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("Grupo Ejemplo Andino", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("<label for=\"bq\">Buscar cliente</label>", nav.Html, StringComparison.Ordinal);

        await nav.GetAsync("/?q=norte");
        Assert.Contains("Ningún cliente coincide con «norte».", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "UX-01 estado de error: si la base no responde se muestra «No se pudo cargar la lista de clientes.»")]
    [Trait("HU", "HU-02")]
    public async Task UX01Error()
    {
        using var app = new FabricaWeb(PerfilSimulado.ABw75, s => s.Replace(ServiceDescriptor.Singleton<IClienteRepository, RepositorioRoto>()));
        var nav = await app.NavegadorAsync();

        await nav.GetAsync("/");

        Assert.Contains("No se pudo cargar la lista de clientes.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Reintentar", nav.Texto, StringComparison.Ordinal);
    }

    // ---------- UX-02 Conexiones (HU-03, HU-04, HU-05, HU-06, HU-57) ----------

    [Fact(DisplayName = "HU-03 CA1: cada sistema tiene su tarjeta con host, mandante, usuario técnico y credencial etiquetados y obligatorios")]
    [Trait("HU", "HU-03")]
    public async Task UX02Tarjetas()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await CrearClienteAsync(nav);

        foreach (var (id, nombre) in new[] { ("erp", "SAP ERP"), ("bw", "SAP BW"), ("bpc", "SAP BPC") })
        {
            Assert.Contains($"<h2 id=\"titulo-{id}\">{nombre}</h2>", nav.Html, StringComparison.Ordinal);
            foreach (var campo in new[] { "host", "mandante", "usuario", "credencial" })
            {
                Assert.Contains($"<label for=\"{id}-{campo}\">", nav.Html, StringComparison.Ordinal);
            }
        }

        Assert.Contains("Sin probar", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Pide al Administrador SAP del cliente", nav.Texto, StringComparison.Ordinal);

        var cliente = nav.Ruta.Replace("/conexiones", "", StringComparison.Ordinal);
        await nav.PostAsync($"{cliente}/conexiones?handler=Probar&sistema=Bw", new Dictionary<string, string?> { ["host"] = "", ["mandante"] = "", ["usuarioTecnico"] = "", ["credencial"] = "" });

        foreach (var error in new[] { "El host es obligatorio.", "El mandante es obligatorio.", "El usuario técnico es obligatorio.", "La credencial es obligatoria." })
        {
            Assert.Contains(error, nav.Texto, StringComparison.Ordinal);
        }

        Assert.Empty(app.Simulador.Llamadas);
    }

    [Fact(DisplayName = "HU-03 CA2: Probar conexión muestra «Conexión exitosa» o «Fallida» con el mensaje de SAP")]
    [Trait("HU", "HU-03")]
    public async Task UX02Probar()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);

        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");
        Assert.Contains("Conexión exitosa", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("SAP ECC 6.0 EHP8", nav.Texto, StringComparison.Ordinal);

        await ProbarAsync(nav, cliente, SistemaSap.Bw, "bw-falla.local", "100");
        Assert.Contains("Fallida", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("No se pudo conectar a SAP BW. El sistema respondió: «Partner 'bw-falla.local:sapgw00' not reached»", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-03 CA3: Continuar al diagnóstico está deshabilitado hasta tener las conexiones exitosas, también en el servidor")]
    [Trait("HU", "HU-03")]
    public async Task UX02ContinuarDeshabilitado()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");

        Assert.Contains("type=\"submit\" disabled=\"disabled\" aria-describedby=\"motivos-continuar\">Continuar al diagnóstico", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Prueba con éxito la conexión a SAP BW.", nav.Texto, StringComparison.Ordinal);

        // Aunque alguien envíe el formulario, el servidor no avanza.
        var r = await nav.PostAsync($"{cliente}/conexiones?handler=Continuar", new Dictionary<string, string?>(), seguir: false);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Aún no puedes continuar al diagnóstico.", nav.Texto, StringComparison.Ordinal);

        await ProbarAsync(nav, cliente, SistemaSap.Bw, "bw-ejemplo.local", "100");
        await ProbarAsync(nav, cliente, SistemaSap.Bpc, "bw-ejemplo.local", "100");
        await nav.PostAsync($"{cliente}/conexiones?handler=Validar", new Dictionary<string, string?>());

        Assert.Contains("<button class=\"btn btn-p\" type=\"submit\">Continuar al diagnóstico</button>", nav.Html, StringComparison.Ordinal);
        r = await nav.PostAsync($"{cliente}/conexiones?handler=Continuar", new Dictionary<string, string?>(), seguir: false);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal($"{cliente}/diagnostico", r.Headers.Location!.OriginalString);
    }

    [Fact(DisplayName = "HU-03 CA4: cada prueba queda en el registro de ejecuciones")]
    [Trait("HU", "HU-03")]
    public async Task UX02Registro()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");
        await ProbarAsync(nav, cliente, SistemaSap.Bw, "bw-falla.local", "100");

        var registro = await app.Services.GetRequiredService<IRegistroEjecuciones>().ListarAsync();

        Assert.Equal(2, registro.Count(e => e.Paso == "Probar conexión"));
        Assert.Contains(registro, e => e.Sistema == "SAP BW" && e.Resultado == ResultadoEjecucion.Fallido);
    }

    [Fact(DisplayName = "HU-04 CA1: la credencial va enmascarada (password), se revela solo con Mostrar y nunca vuelve al navegador")]
    [Trait("HU", "HU-04")]
    public async Task UX02CredencialEnmascarada()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");

        Assert.Contains("id=\"erp-credencial\" name=\"credencial\" type=\"password\" value=\"\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("data-mostrar=\"erp-credencial\" aria-controls=\"erp-credencial\" aria-pressed=\"false\" hidden>Mostrar</button>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Credencial guardada", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(Clave, nav.Html, StringComparison.Ordinal);

        var js = await (await nav.Http.GetAsync("/js/acelerador.js")).Content.ReadAsStringAsync();
        Assert.Contains("campo.type = ver ? 'text' : 'password'", js, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-04 CA2: ni la base SQLite ni el registro de la aplicación ni la configuración contienen la credencial")]
    [Trait("HU", "HU-04")]
    public async Task UX02SinCredencialLegible()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ConectarArquitecturaAAsync(nav, cliente);
        await ProbarAsync(nav, cliente, SistemaSap.Bw, "bw-ejemplo.local", "100", "invalida");
        await nav.PostAsync($"{cliente}/conexiones?handler=Validar", new Dictionary<string, string?>());

        Assert.NotNull(app.Credenciales.Leer(Acelerador.Core.Servicios.ServicioConexiones.ReferenciaCredencial(Guid.Parse(cliente.Split('/')[^1]), SistemaSap.Erp)));

        SqliteConnection.ClearAllPools();
        foreach (var archivo in Directory.GetFiles(app.Carpeta, "*", SearchOption.AllDirectories))
        {
            var bytes = await File.ReadAllBytesAsync(archivo);
            Assert.True(bytes.AsSpan().IndexOf(System.Text.Encoding.UTF8.GetBytes(Clave)) < 0, $"{archivo} contiene la credencial.");
            Assert.True(bytes.AsSpan().IndexOf(System.Text.Encoding.Unicode.GetBytes(Clave)) < 0, $"{archivo} contiene la credencial en UTF-16.");
        }

        Assert.NotEmpty(app.Registro.Lineas);
        Assert.DoesNotContain(app.Registro.Lineas, l => l.Contains(Clave, StringComparison.Ordinal));

        var raiz = Repositorio.Raiz();
        foreach (var config in Directory.GetFiles(Path.Combine(raiz, "src"), "appsettings*.json", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain(Clave, await File.ReadAllTextAsync(config), StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "HU-04 CA4: una credencial inválida se reporta sin escribirla en el mensaje de error")]
    [Trait("HU", "HU-04")]
    public async Task UX02CredencialInvalida()
    {
        using var app = new FabricaWeb();
        app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<OpcionesSimulador>>().CurrentValue.EcoDeCredencialEnErrores = true;
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);

        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");

        Assert.Contains("Name or password is incorrect", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain(Clave, nav.Html, StringComparison.Ordinal);
        Assert.DoesNotContain(app.Registro.Lineas, l => l.Contains(Clave, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "HU-05 CA1: con ERP y BW separados aparece «Validar conexión ERP–BW»")]
    [Trait("HU", "HU-05")]
    public async Task UX02ValidacionVisibleEnA()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);

        Assert.Contains("Validar conexión ERP–BW</button>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("disabled=\"disabled\">Validar conexión ERP–BW", nav.Html, StringComparison.Ordinal);

        await ConectarArquitecturaAAsync(nav, cliente);
        Assert.Contains("<button class=\"btn btn-s\" type=\"submit\">Validar conexión ERP–BW</button>", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-05 CA1: con ERP y BW en el mismo sistema el botón no aparece")]
    [Trait("HU", "HU-05")]
    public async Task UX02ValidacionOcultaEnMismoSistema()
    {
        using var app = new FabricaWeb(PerfilSimulado.B);
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "s4-ejemplo.local", "300");
        await ProbarAsync(nav, cliente, SistemaSap.Bw, "s4-ejemplo.local", "300");

        Assert.DoesNotContain("Validar conexión ERP–BW</button>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("ERP y BW están integrados en el mismo sistema; no requiere validación de conexión ERP–BW.", nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-05 CA2 y CA3: la validación muestra fecha y usuario; si falla, muestra el mensaje y bloquea Continuar")]
    [Trait("HU", "HU-05")]
    public async Task UX02Validacion()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ProbarAsync(nav, cliente, SistemaSap.Erp, "erp-ejemplo.local", "300");
        await ProbarAsync(nav, cliente, SistemaSap.Bw, "bw-sinfuente.local", "100");
        await ProbarAsync(nav, cliente, SistemaSap.Bpc, "bw-sinfuente.local", "100");

        await nav.PostAsync($"{cliente}/conexiones?handler=Validar", new Dictionary<string, string?>());

        Assert.Contains("BW no puede leer del ERP. El sistema respondió: «El sistema fuente ECPCLNT300 no está activo en BW (transacción RSA1).»", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Valida con éxito la conexión ERP–BW.", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("disabled=\"disabled\" aria-describedby=\"motivos-continuar\">Continuar al diagnóstico", nav.Html, StringComparison.Ordinal);

        await ProbarAsync(nav, cliente, SistemaSap.Bw, "bw-ejemplo.local", "100");
        await nav.PostAsync($"{cliente}/conexiones?handler=Validar", new Dictionary<string, string?>());
        Assert.Matches($@"Validada \d\d-\w{{3}}-\d\d \d\d:\d\d por {System.Text.RegularExpressions.Regex.Escape(Environment.UserName)}", nav.Texto);
    }

    [Theory(DisplayName = "HU-06 CA1: UX-02 muestra la insignia A y las versiones de BW y BPC detectadas")]
    [Trait("HU", "HU-06")]
    [InlineData(PerfilSimulado.ABw75, "BW 7.5", "BPC 10.1 modelo estándar", "InfoCubos")]
    [InlineData(PerfilSimulado.ABw4Hana, "BW/4HANA 2.0", "BPC 2021 modelo estándar", "aDSO")]
    public async Task UX02ArquitecturaA(PerfilSimulado perfil, string bw, string bpc, string infoProvider)
    {
        using var app = new FabricaWeb(perfil);
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);

        await ConectarArquitecturaAAsync(nav, cliente);

        Assert.Contains("Arquitectura detectada", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("<span class=\"a-badge\">Arquitectura A</span>", nav.Html, StringComparison.Ordinal);
        Assert.Contains($"Versión de BW: {bw}", nav.Texto, StringComparison.Ordinal);
        Assert.Contains($"Versión de BPC: {bpc}", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Variante disponible", nav.Texto, StringComparison.Ordinal);
        Assert.Contains(infoProvider, nav.Texto, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-06 CA3: sin variante se muestra «Versión no soportada aún» y no se puede continuar")]
    [Trait("HU", "HU-06")]
    public async Task UX02VersionNoSoportada()
    {
        using var app = new FabricaWeb(PerfilSimulado.AVersionNoSoportada);
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        await ConectarArquitecturaAAsync(nav, cliente);
        await nav.PostAsync($"{cliente}/conexiones?handler=Validar", new Dictionary<string, string?>());

        Assert.Contains("Versión no soportada aún", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("BW 7.4 con BPC 10.0 modelo estándar no tiene variante", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("disabled=\"disabled\" aria-describedby=\"motivos-continuar\">Continuar al diagnóstico", nav.Html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "HU-57 CA1, CA2 y CA3: arquitectura B: mensaje y explicación, no se continúa ni se configura, y UX-01 la muestra «No soportado»")]
    [Trait("HU", "HU-57")]
    public async Task UX02ArquitecturaB()
    {
        using var app = new FabricaWeb(PerfilSimulado.B);
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);
        foreach (var s in Enum.GetValues<SistemaSap>())
        {
            await ProbarAsync(nav, cliente, s, "s4-ejemplo.local", "300");
        }

        // CA1
        Assert.Contains("<span class=\"a-badge\">Arquitectura B</span>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Arquitectura B no soportada en la versión 1", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("ERP, BW y BPC están integrados en el mismo sistema", nav.Texto, StringComparison.Ordinal);

        // CA2
        Assert.Contains("disabled=\"disabled\" aria-describedby=\"motivos-continuar\">Continuar al diagnóstico", nav.Html, StringComparison.Ordinal);
        var r = await nav.GetAsync($"{cliente}/modulos");
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal($"{cliente}/conexiones", r.Headers.Location!.OriginalString);
        Assert.DoesNotContain(app.Simulador.Llamadas, l => l.EsEscritura);

        // CA3
        await nav.GetAsync("/");
        Assert.Contains("No soportado", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Arquitectura B no soportada en la versión 1 (RN-19).", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("B · S/4HANA, BW y BPC embebidos", nav.Texto, StringComparison.Ordinal);
    }

    // ---------- UX-04 Módulos (HU-08) ----------

    private static async Task<string> LlegarAModulosAsync(Navegador nav)
    {
        var cliente = await CrearClienteAsync(nav);
        await ConectarArquitecturaAAsync(nav, cliente);
        await nav.PostAsync($"{cliente}/conexiones?handler=Validar", new Dictionary<string, string?>());
        await nav.PostAsync($"{cliente}/conexiones?handler=Continuar", new Dictionary<string, string?>());
        Assert.Contains("Diagnóstico e inventario", nav.Texto, StringComparison.Ordinal);
        await nav.PostAsync($"{cliente}/diagnostico?handler=Escanear", new Dictionary<string, string?>());
        await nav.PostAsync($"{cliente}/diagnostico?handler=Campos", new Dictionary<string, string?> { ["accion"] = "continuar" });
        Assert.Contains("<h1>Módulos</h1>", nav.Html, StringComparison.Ordinal);
        return cliente;
    }

    [Fact(DisplayName = "HU-08 CA1 y CA4: cada módulo es una tarjeta con casilla y descripción; activos fijos y deterioros se quedan en BW")]
    [Trait("HU", "HU-08")]
    public async Task UX04Tarjetas()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        await LlegarAModulosAsync(nav);

        foreach (var f in CatalogoModulos.Todos)
        {
            Assert.Contains($"<label class=\"card mod\" for=\"md-{f.Modulo}\">", nav.Html, StringComparison.Ordinal);
            Assert.Contains($"type=\"checkbox\" name=\"modulos\" value=\"{f.Modulo}\" data-reportes=\"{f.Reportes}\"", nav.Html, StringComparison.Ordinal);
            Assert.Contains(f.Genera, nav.Texto, StringComparison.Ordinal);
        }

        Assert.Equal(2, nav.Texto.Split("Se queda en BW; no crea modelo en BPC.").Length - 1);
    }

    [Fact(DisplayName = "HU-08 CA2 y CA3: el resumen cuenta módulos y reportes; Continuar está deshabilitado sin módulos")]
    [Trait("HU", "HU-08")]
    public async Task UX04ResumenYContinuar()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await LlegarAModulosAsync(nav);

        Assert.Contains("0 módulos · 0 reportes", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("value=\"continuar\" disabled=\"disabled\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("Elige al menos un módulo.", nav.Texto, StringComparison.Ordinal);

        // Sin módulos, el servidor tampoco deja continuar.
        var r = await nav.PostAsync($"{cliente}/modulos", new Dictionary<string, string?> { ["accion"] = "continuar" }, seguir: false);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Contains("Elige al menos un módulo.", nav.Texto, StringComparison.Ordinal);

        await nav.PostAsync($"{cliente}/modulos", new Dictionary<string, string?> { ["modulos"] = "Consolidacion", ["accion"] = "guardar" });
        Assert.Contains("1 módulo · 24 reportes", nav.Texto, StringComparison.Ordinal);
        Assert.Contains("Selección guardada.", nav.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("value=\"continuar\" disabled", nav.Html, StringComparison.Ordinal);

        var js = await (await nav.Http.GetAsync("/js/acelerador.js")).Content.ReadAsStringAsync();
        Assert.Contains("continuar.disabled = n === 0", js, StringComparison.Ordinal);

        r = await nav.PostAsync($"{cliente}/modulos", new Dictionary<string, string?> { ["modulos"] = "Presupuesto", ["accion"] = "continuar" }, seguir: false);
        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal($"{cliente}/sociedades", r.Headers.Location!.OriginalString);
    }

    [Fact(DisplayName = "Flujo: un paso al que el cliente no ha llegado redirige a su paso actual")]
    [Trait("HU", "HU-08")]
    public async Task UX04SinLlegar()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);

        var r = await nav.GetAsync($"{cliente}/modulos");

        Assert.Equal(HttpStatusCode.Redirect, r.StatusCode);
        Assert.Equal($"{cliente}/conexiones", r.Headers.Location!.OriginalString);
    }

    [Fact(DisplayName = "WCAG 2.1 AA básico: idioma, salto al contenido, navegación con paso actual y estados con texto")]
    public async Task Accesibilidad()
    {
        using var app = new FabricaWeb();
        var nav = await app.NavegadorAsync();
        var cliente = await CrearClienteAsync(nav);

        Assert.Contains("<html lang=\"es\">", nav.Html, StringComparison.Ordinal);
        Assert.Contains("<a class=\"saltar\" href=\"#contenido\">Saltar al contenido</a>", nav.Html, StringComparison.Ordinal);
        Assert.Contains("<main class=\"a-main\" id=\"contenido\">", nav.Html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"Pasos de la implementación\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("aria-current=\"step\"", nav.Html, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", nav.Html, StringComparison.Ordinal);

        var css = await (await nav.Http.GetAsync("/css/acelerador.css")).Content.ReadAsStringAsync();
        Assert.Contains(":focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", css, StringComparison.Ordinal);
        Assert.Contains("[hidden] { display: none !important; }", css, StringComparison.Ordinal);
    }

    /// <summary>Repositorio que falla siempre, para el estado de error de UX-01.</summary>
    private sealed class RepositorioRoto : IClienteRepository
    {
        public Task<IReadOnlyList<Cliente>> ListarAsync(CancellationToken ct = default) => throw new SqliteException("disk I/O error", 10);

        public Task<Cliente?> ObtenerAsync(Guid id, CancellationToken ct = default) => throw new SqliteException("disk I/O error", 10);

        public Task CrearAsync(Cliente cliente, CancellationToken ct = default) => throw new SqliteException("disk I/O error", 10);

        public Task ActualizarAsync(Cliente cliente, CancellationToken ct = default) => throw new SqliteException("disk I/O error", 10);
    }
}
