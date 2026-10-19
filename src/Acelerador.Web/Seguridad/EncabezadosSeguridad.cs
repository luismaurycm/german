using Microsoft.AspNetCore.Diagnostics;

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

    /// <summary>
    /// ASVS V12.1.1 y V13.1.3: rechaza con 413 una petición que declara más de <paramref name="limite"/> bytes, salvo la subida
    /// del archivo no SAP, que tiene su propio límite. Kestrel aplica el mismo límite a las peticiones sin Content-Length.
    /// </summary>
    public static IApplicationBuilder UseLimitePeticiones(this IApplicationBuilder app, long limite, long limiteArchivo) =>
        app.Use(async (contexto, siguiente) =>
        {
            // La página de error re-ejecuta la petición original: su tamaño ya se controló en la primera pasada.
            if (contexto.Features.Get<IStatusCodeReExecuteFeature>() is not null || contexto.Features.Get<IExceptionHandlerFeature>() is not null)
            {
                await siguiente(contexto);
                return;
            }

            var subida = contexto.Request.Path.Value?.EndsWith("/no-sap", StringComparison.OrdinalIgnoreCase) == true
                         && string.Equals(contexto.Request.Query["handler"], "Subir", StringComparison.OrdinalIgnoreCase);
            if (contexto.Request.ContentLength > (subida ? limiteArchivo : limite))
            {
                contexto.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                contexto.Response.ContentType = "text/plain; charset=utf-8";
                await contexto.Response.WriteAsync("La petición es demasiado grande.");
                return;
            }

            // La validación antiforgery lee el formulario antes que [RequestSizeLimit]: la subida sube su límite aquí.
            var tamano = contexto.Features.Get<Microsoft.AspNetCore.Http.Features.IHttpMaxRequestBodySizeFeature>();
            if (tamano is { IsReadOnly: false })
            {
                tamano.MaxRequestBodySize = subida ? limiteArchivo : limite;
            }

            await siguiente(contexto);
        });
}
