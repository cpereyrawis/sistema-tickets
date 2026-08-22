namespace Asistente.Api.Desarrollo;

/// <summary>
/// Manda al navegador a la aplicación.
///
/// En desarrollo hay dos servidores: esta API y el de Vite. Visual Studio abre el
/// navegador en la dirección de la API, que por sí sola no tiene nada que mostrar. Esto
/// traduce esa primera visita a la dirección del frontend, de modo que la pestaña que se
/// abre ya es la aplicación.
///
/// Solo se redirige la NAVEGACIÓN: peticiones GET de un navegador pidiendo HTML. Las
/// llamadas a la API y a Swagger siguen de largo, porque el frontend las proxea de vuelta
/// hacia acá y redirigirlas armaría un círculo.
/// </summary>
public static class RedireccionFrontend
{
    private static readonly string[] RutasPropias = ["/api", "/swagger"];

    public static IApplicationBuilder UsarRedireccionAlFrontend(
        this IApplicationBuilder app, FrontendSettings config)
    {
        if (!config.Habilitado) return app;

        return app.Use(async (contexto, siguiente) =>
        {
            if (EsNavegacion(contexto.Request))
            {
                var destino = config.Url.TrimEnd('/')
                    + contexto.Request.Path
                    + contexto.Request.QueryString;

                contexto.Response.Redirect(destino);
                return;
            }

            await siguiente();
        });
    }

    private static bool EsNavegacion(HttpRequest peticion)
    {
        if (!HttpMethods.IsGet(peticion.Method)) return false;

        foreach (var ruta in RutasPropias)
        {
            if (peticion.Path.StartsWithSegments(ruta, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // El navegador pide HTML; fetch y XHR piden JSON. Es lo que distingue a alguien
        // escribiendo la dirección de la propia aplicación llamando a un endpoint.
        return peticion.Headers.Accept.Any(
            v => v is not null && v.Contains("text/html", StringComparison.OrdinalIgnoreCase));
    }
}
