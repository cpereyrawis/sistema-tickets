using System.Security.Claims;
using Asistente.Common;
using Asistente.Domain.Dtos;
using Asistente.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Asistente.Api.Controllers;

/// <summary>
/// Inicio de sesión, sesión vigente y cambio de la propia contraseña.
///
/// No hay registro ni recuperación por correo: las cuentas se precargan y quien pierde su
/// contraseña la pide a alguien con permiso de mantenimiento.
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

        return Ok(await ArmarSesionAsync(identidad.Id, ct));
    }

    /// <summary>Sesión vigente, para que el frontend sepa si sigue autenticado al recargar.</summary>
    [HttpGet("sesion")]
    [ProducesResponseType<SesionUsuarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Sesion(CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(id, out var userId)) return Unauthorized();

        var dto = await ArmarSesionAsync(userId, ct);
        return dto is null ? Unauthorized() : Ok(dto);
    }

    /// <summary>Cambio de la propia contraseña. Exige la actual.</summary>
    [HttpPost("cambiar-clave")]
    [Authorize]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> CambiarClave([FromBody] CambioClaveRequest request, CancellationToken ct)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!long.TryParse(id, out var userId)) return Unauthorized();

        var resultado = await _auth.CambiarClavePropiaAsync(userId, request, ct);

        return resultado.Ok
            ? Ok(new { cambiada = true })
            : Problem(title: resultado.Mensaje, statusCode: StatusPara(resultado.Codigo), type: resultado.Codigo);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    /// <summary>
    /// Arma el DTO de sesión con los permisos vigentes. Se leen de la base en cada
    /// petición y no del contenido de la cookie: si a alguien se le retira una atribución,
    /// tiene que dejar de tenerla ya, no cuando venza su sesión.
    /// </summary>
    private async Task<SesionUsuarioDto?> ArmarSesionAsync(long userId, CancellationToken ct)
    {
        var usuario = await _usuarios.BuscarPorIdAsync(userId, ct);
        if (usuario is null || !usuario.Activo) return null;

        var permisos = await _usuarios.ListarPermisosAsync(userId, ct);

        return new SesionUsuarioDto(usuario.Id, usuario.Usuario, usuario.NombreCompleto, permisos);
    }

    private static int StatusPara(string? codigo) => codigo switch
    {
        CodigosError.CredencialesInvalidas => StatusCodes.Status401Unauthorized,
        CodigosError.CuentaInactiva => StatusCodes.Status403Forbidden,
        CodigosError.NoAutenticado => StatusCodes.Status401Unauthorized,

        // 429 comunica que hay que esperar, sin decir si la cuenta existe.
        CodigosError.CuentaBloqueada => StatusCodes.Status429TooManyRequests,

        _ => StatusCodes.Status400BadRequest,
    };
}
