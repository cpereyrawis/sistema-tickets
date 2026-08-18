using System.Security.Claims;
using Asistente.Common;
using Asistente.Domain.Dtos;
using Asistente.Domain.Services;
using Asistente.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Asistente.Api.Controllers;

/// <summary>
/// Registro, inicio de sesión, verificación de correo y restablecimiento de contraseña.
///
/// La sesión se sostiene con una cookie cifrada por el servidor. La contraseña se descarta
/// apenas se valida y nunca vuelve al cliente en ninguna forma (FR-003, AC-16).
/// </summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    private readonly IUsuarioRepository _usuarios;

    public AuthController(IAuthService auth, IUsuarioRepository usuarios)
    {
        _auth = auth;
        _usuarios = usuarios;
    }

    /// <summary>
    /// Nombres habilitados para registrarse. Es una lista cerrada: la aplicación es
    /// interna y nadie de afuera debe poder crearse una cuenta.
    /// </summary>
    [HttpGet("usuarios-habilitados")]
    [ProducesResponseType<IReadOnlyList<UsuarioHabilitadoDto>>(StatusCodes.Status200OK)]
    public IActionResult UsuariosHabilitadosDisponibles() =>
        Ok(UsuariosHabilitados.Todos
            .Select(u => new UsuarioHabilitadoDto(u.Usuario, u.NombreCompleto))
            .ToList());

    /// <summary>Dominio de correo que la interfaz muestra fijo y no editable.</summary>
    [HttpGet("dominio-correo")]
    public IActionResult DominioCorreo() => Ok(new { dominio = UsuariosHabilitados.DominioCorreo });

    /// <summary>Crea la cuenta y envía el correo de activación. No inicia sesión.</summary>
    [HttpPost("registro")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<RegistroResultado>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Registro([FromBody] RegistroRequest request, CancellationToken ct)
    {
        var resultado = await _auth.RegistrarAsync(request, ct);
        return resultado.Ok
            ? Ok(resultado.Valor)
            : Problem(title: resultado.Mensaje, statusCode: StatusPara(resultado.Codigo), type: resultado.Codigo);
    }

    /// <summary>Valida las credenciales y emite la cookie de sesión.</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType<SesionUsuarioDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var resultado = await _auth.IniciarSesionAsync(request, ct);

        if (!resultado.Ok)
        {
            return Problem(
                title: resultado.Mensaje, statusCode: StatusPara(resultado.Codigo), type: resultado.Codigo);
        }

        var identidad = resultado.Valor!;
        var usuario = await _usuarios.BuscarPorIdAsync(identidad.Id, ct);
        if (usuario is null) return Unauthorized();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identidad.Id.ToString()),
            new(ClaimTypes.Name, identidad.Usuario),
        };

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                // Sesión de trabajo, no "recordarme": la cookie muere al cerrar el navegador
                // y se renueva sola mientras haya actividad.
                IsPersistent = false,
                AllowRefresh = true,
            });

        return Ok(new SesionUsuarioDto(
            usuario.Id, usuario.Usuario, usuario.NombreCompleto, usuario.Email));
    }

    /// <summary>Sesión vigente, para que el frontend sepa si sigue autenticado al recargar.</summary>
    [HttpGet("sesion")]
    [ProducesResponseType<SesionUsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Sesion(CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(id, out var userId)) return Unauthorized();

        var usuario = await _usuarios.BuscarPorIdAsync(userId, ct);
        if (usuario is null || !usuario.Activo) return Unauthorized();

        return Ok(new SesionUsuarioDto(
            usuario.Id, usuario.Usuario, usuario.NombreCompleto, usuario.Email));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>Activa la cuenta con el token del correo.</summary>
    [HttpPost("verificar-email")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerificarEmail([FromBody] TokenRequest request, CancellationToken ct)
    {
        var resultado = await _auth.VerificarEmailAsync(request.Token, ct);
        return resultado.Ok
            ? Ok(new { verificado = true })
            : Problem(title: resultado.Mensaje, statusCode: StatusPara(resultado.Codigo), type: resultado.Codigo);
    }

    /// <summary>
    /// Envía el enlace de restablecimiento. Responde siempre lo mismo, exista o no la
    /// cuenta: distinguirlas convertiría este endpoint en un verificador de correos.
    /// </summary>
    [HttpPost("olvido-clave")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> OlvidoClave([FromBody] OlvidoClaveRequest request, CancellationToken ct)
    {
        await _auth.SolicitarRestablecerAsync(request, ct);

        return Ok(new
        {
            mensaje = "Si el correo corresponde a una cuenta, te enviamos un enlace para restablecer la contraseña.",
        });
    }

    /// <summary>Fija la nueva contraseña usando el token del correo.</summary>
    [HttpPost("restablecer-clave")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> RestablecerClave(
        [FromBody] RestablecerClaveRequest request, CancellationToken ct)
    {
        var resultado = await _auth.RestablecerClaveAsync(request, ct);
        return resultado.Ok
            ? Ok(new { restablecida = true })
            : Problem(title: resultado.Mensaje, statusCode: StatusPara(resultado.Codigo), type: resultado.Codigo);
    }

    public sealed record TokenRequest(string Token);

    private static int StatusPara(string? codigo) => codigo switch
    {
        CodigosError.CredencialesInvalidas => StatusCodes.Status401Unauthorized,
        CodigosError.EmailNoVerificado => StatusCodes.Status403Forbidden,
        CodigosError.CuentaInactiva => StatusCodes.Status403Forbidden,

        // 429 comunica que hay que esperar, sin decir si la cuenta existe.
        CodigosError.CuentaBloqueada => StatusCodes.Status429TooManyRequests,

        CodigosError.UsuarioYaRegistrado => StatusCodes.Status409Conflict,
        CodigosError.TokenInvalido => StatusCodes.Status400BadRequest,
        CodigosError.ClaveInvalida => StatusCodes.Status400BadRequest,
        CodigosError.EmailInvalido => StatusCodes.Status400BadRequest,
        CodigosError.UsuarioNoHabilitado => StatusCodes.Status400BadRequest,

        _ => StatusCodes.Status400BadRequest,
    };
}
