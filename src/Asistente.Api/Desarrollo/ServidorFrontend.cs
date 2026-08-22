using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Asistente.Api.Desarrollo;

/// <summary>Dónde vive el servidor de desarrollo del frontend y cómo se arranca.</summary>
public sealed class FrontendSettings
{
    public const string SectionName = "FrontendDev";

    /// <summary>Permite arrancar solo la API, sin levantar el servidor de Vite.</summary>
    public bool Habilitado { get; set; } = true;

    /// <summary>Debe coincidir con el puerto declarado en vite.config.ts.</summary>
    public string Url { get; set; } = "http://localhost:5173";

    /// <summary>Ruta al proyecto del cliente, relativa a la raíz del contenido.</summary>
    public string Directorio { get; set; } = "../asistente.client";

    public string Comando { get; set; } = "npm run dev";

    /// <summary>Cuánto se espera a que el servidor responda antes de darlo por fallido.</summary>
    public int SegundosEspera { get; set; } = 90;
}

/// <summary>
/// Arranca el servidor de desarrollo del frontend junto con la API.
///
/// Reemplaza a Microsoft.AspNetCore.SpaProxy, que hacía lo mismo pero lanzando el comando
/// con <c>UseShellExecute</c>: eso abre una ventana de consola aparte que queda dando
/// vueltas en el escritorio. Acá el proceso se lanza SIN ventana y su salida se redirige
/// al log de la aplicación, así que lo que Vite tenga para decir aparece junto al resto
/// en lugar de en una terminal separada.
///
/// Solo se registra en desarrollo. En producción el frontend se sirve compilado desde la
/// propia aplicación y no hay ningún servidor de Node que levantar.
/// </summary>
public sealed class ServidorFrontend : IHostedService
{
    private readonly FrontendSettings _config;
    private readonly IHostEnvironment _entorno;
    private readonly ILogger<ServidorFrontend> _logger;
    private Process? _proceso;

    public ServidorFrontend(
        FrontendSettings config, IHostEnvironment entorno, ILogger<ServidorFrontend> logger)
    {
        _config = config;
        _entorno = entorno;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (!_config.Habilitado)
        {
            _logger.LogInformation("Arranque del frontend desactivado por configuración.");
            return;
        }

        // Si ya hay algo escuchando, es que el servidor quedó de una corrida anterior o
        // alguien lo levantó a mano. Se reutiliza en lugar de pelear por el puerto.
        if (await RespondeAsync(ct))
        {
            _logger.LogInformation("El frontend ya estaba escuchando en {Url}.", _config.Url);
            return;
        }

        var directorio = Path.GetFullPath(Path.Combine(_entorno.ContentRootPath, _config.Directorio));

        if (!Directory.Exists(directorio))
        {
            _logger.LogWarning(
                "No se encontró el proyecto del cliente en {Directorio}; no se levanta el frontend.",
                directorio);
            return;
        }

        _proceso = LanzarSinVentana(directorio);

        if (_proceso is null) return;

        _logger.LogInformation("Levantando el frontend en {Url}…", _config.Url);
        await EsperarAQueRespondaAsync(ct);
    }

    /// <summary>
    /// En Windows <c>npm</c> es un archivo .cmd y no un ejecutable, así que hay que
    /// invocarlo a través del intérprete de comandos. La combinación de
    /// <c>UseShellExecute = false</c> con <c>CreateNoWindow = true</c> es la que evita que
    /// aparezca la ventana; con shell execute, Windows la abre igual.
    /// </summary>
    private Process? LanzarSinVentana(string directorio)
    {
        var esWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        var inicio = new ProcessStartInfo
        {
            FileName = esWindows ? "cmd.exe" : "/bin/sh",
            WorkingDirectory = directorio,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        inicio.ArgumentList.Add(esWindows ? "/c" : "-c");
        inicio.ArgumentList.Add(_config.Comando);

        try
        {
            var proceso = Process.Start(inicio);
            if (proceso is null)
            {
                _logger.LogError("No se pudo iniciar el frontend con «{Comando}».", _config.Comando);
                return null;
            }

            proceso.OutputDataReceived += (_, e) => Registrar(e.Data, esError: false);
            proceso.ErrorDataReceived += (_, e) => Registrar(e.Data, esError: true);
            proceso.BeginOutputReadLine();
            proceso.BeginErrorReadLine();

            return proceso;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló el arranque del frontend. ¿Está instalado Node.js?");
            return null;
        }
    }

    private void Registrar(string? linea, bool esError)
    {
        if (string.IsNullOrWhiteSpace(linea)) return;

        // Vite escribe avisos por la salida de error que no son fallos, así que se
        // registran como información: marcarlos como errores sería ruido que enseña a
        // ignorar los errores de verdad.
        if (esError) _logger.LogWarning("[frontend] {Linea}", linea);
        else _logger.LogInformation("[frontend] {Linea}", linea);
    }

    private async Task EsperarAQueRespondaAsync(CancellationToken ct)
    {
        var limite = DateTime.UtcNow.AddSeconds(_config.SegundosEspera);

        while (DateTime.UtcNow < limite)
        {
            if (ct.IsCancellationRequested) return;
            if (await RespondeAsync(ct))
            {
                _logger.LogInformation("Frontend listo en {Url}.", _config.Url);
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
        }

        _logger.LogWarning(
            "El frontend no respondió en {Segundos} segundos. La API funciona igual.",
            _config.SegundosEspera);
    }

    private async Task<bool> RespondeAsync(CancellationToken ct)
    {
        try
        {
            using var cliente = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            using var respuesta = await cliente.GetAsync(_config.Url, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        if (_proceso is null || _proceso.HasExited) return Task.CompletedTask;

        try
        {
            // El árbol completo: se lanzó cmd, que a su vez lanzó node. Matar solo el
            // padre dejaría el servidor de Vite ocupando el puerto para la próxima vez.
            _proceso.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo detener el proceso del frontend.");
        }

        return Task.CompletedTask;
    }
}
