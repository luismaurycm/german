namespace Acelerador.Web.Seguridad;

/// <summary>Encabezados de endurecimiento (RNF-Seg-01): sin recursos externos, sin marcos, sin caché de páginas.</summary>
public static class EncabezadosSeguridad
{
    public const string Csp =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
        "font-src 'self'; connect-src 'self'; form-action 'self'; frame-ancestors 'none'; base-uri 'none'; object-src 'none'";

    public static IApplicationBuilder UseEncabezadosSeguridad(this IApplicationBuilder app) =>
        app.Use(async (contexto, siguiente) =>
        {
            contexto.Response.OnStarting(() =>
            {
                var h = contexto.Response.Headers;
                h.ContentSecurityPolicy = Csp;
                h.XContentTypeOptions = "nosniff";
                h.XFrameOptions = "DENY";
                h["Referrer-Policy"] = "no-referrer";
                h["Cross-Origin-Opener-Policy"] = "same-origin";
                h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                if (!contexto.Request.Path.StartsWithSegments("/css") && !contexto.Request.Path.StartsWithSegments("/js"))
                {
                    h.CacheControl = "no-store";
                }

                return Task.CompletedTask;
            });
            await siguiente(contexto);
        });
}
