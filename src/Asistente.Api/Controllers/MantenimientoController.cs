using System.Security.Claims;
using Asistente.Api.Security;
using Asistente.Common;
using Asistente.Domain.Dtos;
using Asistente.Domain.Entities;
using Asistente.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Api.Controllers;

/// <summary>
/// Mantenimiento de Usuarios: operar sobre cuentas ajenas.
///
/// Cada acción exige su propio permiso, no un rol genérico de administrador. Así se puede
/// dar a alguien la atribución de destrabar cuentas —que es rutina y de bajo riesgo— sin
/// darle además la de cambiar contraseñas, que es con lo que se suplanta a una persona.
/// </summary>
[ApiController]
[Route("api/mantenimiento")]
[Produces("application/json")]
[Authorize]
public sealed class MantenimientoController : ControllerBase
{
    private readonly IMantenimientoUsuariosService _servicio;

    public MantenimientoController(IMantenimientoUsuariosService servicio) => _servicio = servicio;

    /// <summary>Nómina completa con el estado de cada cuenta.</summary>
    [HttpGet("usuarios")]
    [ExigePermiso(Permisos.UsuarioListar)]
    [ProducesResponseType<IReadOnlyList<UsuarioMantenimientoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Usuarios(CancellationToken ct) =>
        Ok(await _servicio.ListarAsync(ct));

    /// <summary>
    /// Asigna una contraseña nueva. No devuelve ni muestra la anterior porque no existe
    /// forma de obtenerla: lo guardado es un hash.
    /// </summary>
    [HttpPost("usuarios/{id:long}/clave")]
    [ExigePermiso(Permisos.UsuarioResetClave)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ResetClave(
        long id, [FromBody] ResetClaveRequest request, CancellationToken ct)
    {
        var resultado = await _servicio.ResetClaveAsync(EjecutorId(), id, request, ct);

        return resultado.Ok
            ? Ok(new { asignada = true })
            : Problem(title: resultado.Mensaje, statusCode: StatusPara(resultado.Codigo), type: resultado.Codigo);
    }

    /// <summary>Levanta el bloqueo por intentos fallidos sin tocar la contraseña.</summary>
    [HttpPost("usuarios/{id:long}/desbloquear")]
    [ExigePermiso(Permisos.UsuarioDesbloquear)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Desbloquear(long id, CancellationToken ct)
    {
        var resultado = await _servicio.DesbloquearAsync(EjecutorId(), id, ct);

        return resultado.Ok
            ? Ok(new { desbloqueado = true })
            : Problem(title: resultado.Mensaje, statusCode: StatusPara(resultado.Codigo), type: resultado.Codigo);
    }

    private long EjecutorId() =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    private static int StatusPara(string? codigo) => codigo switch
    {
        CodigosError.UsuarioNoEncontrado => StatusCodes.Status404NotFound,
        CodigosError.PermisoDenegado => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest,
    };
}
