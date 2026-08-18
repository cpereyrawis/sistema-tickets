using System.Net;
using System.Net.Mail;
using Asistente.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asistente.Persistence.Services;

public sealed class CorreoSettings
{
    public const string SectionName = "CorreoSettings";

    /// <summary>"Smtp" envía de verdad; "Archivo" escribe el correo en disco.</summary>
    public string Proveedor { get; set; } = "Archivo";

    public string Host { get; set; } = string.Empty;
    public int Puerto { get; set; } = 587;
    public bool UsarSsl { get; set; } = true;
    public string Usuario { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;

    public string RemitenteEmail { get; set; } = "no-reply@wis-software.com";
    public string RemitenteNombre { get; set; } = "Asistente de Registro";

    /// <summary>Carpeta donde el proveedor "Archivo" deja los correos.</summary>
    public string CarpetaSalida { get; set; } = "correos-enviados";
}

/// <summary>
/// Envío real por SMTP.
///
/// Usa <see cref="SmtpClient"/> del framework, que alcanza para mandar correo
/// transaccional a un relay corporativo. Está marcado como obsoleto para escenarios
/// complejos —autenticación moderna, OAuth2, lectura de buzones—; si el servidor exige
/// algo de eso, el reemplazo natural es MailKit y solo cambia esta clase.
/// </summary>
public sealed class ServicioCorreoSmtp : IServicioCorreo
{
    private readonly CorreoSettings _config;
    private readonly ILogger<ServicioCorreoSmtp> _logger;

    public ServicioCorreoSmtp(IOptions<CorreoSettings> opciones, ILogger<ServicioCorreoSmtp> logger)
    {
        _config = opciones.Value;
        _logger = logger;
    }

    public async Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml, CancellationToken ct)
    {
        using var cliente = new SmtpClient(_config.Host, _config.Puerto)
        {
            EnableSsl = _config.UsarSsl,
            Credentials = string.IsNullOrWhiteSpace(_config.Usuario)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_config.Usuario, _config.Clave),
        };

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_config.RemitenteEmail, _config.RemitenteNombre),
            Subject = asunto,
            Body = cuerpoHtml,
            IsBodyHtml = true,
        };
        mensaje.To.Add(destinatario);

        try
        {
            await cliente.SendMailAsync(mensaje, ct);
            _logger.LogInformation("Correo enviado a {Destinatario}.", destinatario);
        }
        catch (Exception ex)
        {
            // No se relanza: que el relay falle no debe tumbar un registro ya confirmado.
            // Queda registrado para poder reenviarlo, y sin el cuerpo, que lleva el enlace.
            _logger.LogError(ex, "No se pudo enviar el correo a {Destinatario}.", destinatario);
        }
    }
}

/// <summary>
/// Escribe cada correo en un archivo .html en lugar de enviarlo.
///
/// Es el proveedor de DESARROLLO: permite probar el circuito completo de activación y
/// restablecimiento sin servidor SMTP ni buzones reales. Se abre el archivo y se hace clic
/// en el enlace igual que en un cliente de correo.
/// </summary>
public sealed class ServicioCorreoArchivo : IServicioCorreo
{
    private readonly CorreoSettings _config;
    private readonly ILogger<ServicioCorreoArchivo> _logger;

    public ServicioCorreoArchivo(IOptions<CorreoSettings> opciones, ILogger<ServicioCorreoArchivo> logger)
    {
        _config = opciones.Value;
        _logger = logger;
    }

    public async Task EnviarAsync(
        string destinatario, string asunto, string cuerpoHtml, CancellationToken ct)
    {
        Directory.CreateDirectory(_config.CarpetaSalida);

        var nombre = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Sanear(destinatario)}.html";
        var ruta = Path.Combine(_config.CarpetaSalida, nombre);

        var contenido =
            $"<!doctype html><meta charset=\"utf-8\"><title>{asunto}</title>"
            + $"<p style=\"font-family:monospace;color:#888\">Para: {destinatario}<br>Asunto: {asunto}</p><hr>"
            + cuerpoHtml;

        await File.WriteAllTextAsync(ruta, contenido, ct);

        _logger.LogWarning(
            "CORREO NO ENVIADO (proveedor Archivo). Escrito en {Ruta}. Destinatario: {Destinatario}.",
            Path.GetFullPath(ruta), destinatario);
    }

    private static string Sanear(string valor) =>
        string.Concat(valor.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
