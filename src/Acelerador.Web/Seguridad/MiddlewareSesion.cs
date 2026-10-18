using Microsoft.AspNetCore.Http.Extensions;

namespace Acelerador.Web.Seguridad;

/// <summary>
/// Exige el token de <see cref="SesionLocal"/> en cada petición (ADR-05, RNF-Seg-01).
/// <list type="bullet">
/// <item>Con <c>?t=token</c> válido: crea la cookie y redirige a la misma ruta sin el token, para que no quede en el historial.</item>
/// <item>Con la cookie válida: deja pasar.</item>
/// <item>En otro caso: 401 con instrucciones.</item>
/// </list>
/// </summary>
public sealed class MiddlewareSesion(RequestDelegate siguiente, SesionLocal sesion)
{
    public async Task InvokeAsync(HttpContext contexto)
    {
        var solicitud = contexto.Request;

        if (solicitud.Query.TryGetValue(SesionLocal.ParametroToken, out var token))
        {
            if (!sesion.EsValido(token.ToString()))
            {
                await Rechazar(contexto);
                return;
            }

            contexto.Response.Cookies.Append(SesionLocal.NombreCookie, sesion.Token, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Strict,
                Secure = solicitud.IsHttps,
                Path = "/",
                IsEssential = true,
            });

            var resto = new QueryBuilder(solicitud.Query
                .Where(q => q.Key != SesionLocal.ParametroToken)
                .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string>(q.Key, v ?? ""))));
            contexto.Response.Redirect($"{solicitud.PathBase}{solicitud.Path}{resto}");
            return;
        }

        if (!sesion.EsValido(solicitud.Cookies[SesionLocal.NombreCookie]))
        {
            await Rechazar(contexto);
            return;
        }

        await siguiente(contexto);
    }

    private static Task Rechazar(HttpContext contexto)
    {
        contexto.Response.StatusCode = StatusCodes.Status401Unauthorized;
        contexto.Response.ContentType = "text/html; charset=utf-8";
        return contexto.Response.WriteAsync("""
            <!doctype html>
            <html lang="es"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Sesión no válida · Acelerador</title><link rel="stylesheet" href="/css/acelerador.css"></head>
            <body><main class="a-main solo" id="contenido">
            <div role="alert" class="note e"><svg width="20" height="20" viewBox="0 0 20 20" aria-hidden="true"><circle cx="10" cy="10" r="9" fill="#B3261E"/><path d="M10 5.5v5.5M10 14v.5" stroke="#fff" stroke-width="2.2" stroke-linecap="round"/></svg>
            <div><b>Sesión no válida.</b><br>Abre el acelerador con el enlace que muestra la ventana de la aplicación al iniciar.
            Si la cerraste, vuelve a abrir el acelerador.</div></div>
            </main></body></html>
            """);
    }
}
