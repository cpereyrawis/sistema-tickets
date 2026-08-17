using Asistente.Api.Security;
using Asistente.Common;
using Asistente.Domain.Dtos;
using Asistente.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Api.Controllers;

/// <summary>
/// Transiciones de la jornada.
///
/// Todas las operaciones devuelven el MISMO objeto de estado que <c>GET current</c>: el
/// frontend nunca infiere en qué estado quedó la jornada, lo recibe. Eso elimina la clase
/// de errores donde la interfaz y el servidor discrepan.
/// </summary>
[ApiController]
[Route("api/jornada")]
[Produces("application/json")]
public sealed class JornadaController : ControllerBase
{
    private readonly IWorkdayService _servicio;
    private readonly IUsuarioActual _usuario;

    public JornadaController(IWorkdayService servicio, IUsuarioActual usuario)
    {
        _servicio = servicio;
        _usuario = usuario;
    }

    /// <summary>Estado vigente de la jornada y acciones válidas para ese estado.</summary>
    [HttpGet("actual")]
    [ProducesResponseType<EstadoJornadaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Actual(CancellationToken ct)
    {
        if (_usuario.UserId is not { } userId) return Unauthorized();
        return Ok(await _servicio.ObtenerEstadoAsync(userId, ct));
    }

    /// <summary>Comenzar el día seleccionando el ticket de la primera tarea principal.</summary>
    [HttpPost("comenzar")]
    public Task<IActionResult> Comenzar([FromBody] ComenzarDiaRequest request, CancellationToken ct) =>
        Ejecutar((userId, c) => _servicio.ComenzarDiaAsync(userId, request, c), ct);

    /// <summary>Cierra la tarea vigente e inicia la siguiente en la misma marca temporal.</summary>
    [HttpPost("fin-tarea")]
    public Task<IActionResult> FinTarea([FromBody] FinTareaRequest request, CancellationToken ct) =>
        Ejecutar((userId, c) => _servicio.FinTareaAsync(userId, request, c), ct);

    /// <summary>Registra una interrupción: cuatro eventos y reanudación automática.</summary>
    [HttpPost("interrupcion")]
    public Task<IActionResult> Interrupcion([FromBody] InterrupcionRequest request, CancellationToken ct) =>
        Ejecutar((userId, c) => _servicio.RegistrarInterrupcionAsync(userId, request, c), ct);

    /// <summary>Salida al descanso: cierra la sesión principal sin imputar tiempo.</summary>
    [HttpPost("descanso/salida")]
    public Task<IActionResult> SalidaDescanso(CancellationToken ct) =>
        Ejecutar(_servicio.SalidaDescansoAsync, ct);

    /// <summary>Regreso del descanso: reanuda el mismo ticket principal.</summary>
    [HttpPost("descanso/regreso")]
    public Task<IActionResult> RegresoDescanso(CancellationToken ct) =>
        Ejecutar(_servicio.RegresoDescansoAsync, ct);

    /// <summary>Cierra la sesión y la jornada.</summary>
    [HttpPost("fin-dia")]
    public Task<IActionResult> FinDia([FromBody] FinDiaRequest request, CancellationToken ct) =>
        Ejecutar((userId, c) => _servicio.FinDiaAsync(userId, request, c), ct);

    /// <summary>Reabre una jornada cerrada por error, como corrección auditada.</summary>
    [HttpPost("reabrir")]
    public Task<IActionResult> Reabrir([FromBody] ReabrirRequest request, CancellationToken ct) =>
        Ejecutar((userId, c) => _servicio.ReabrirAsync(userId, request, c), ct);

    // ---------- Apoyo ----------

    private async Task<IActionResult> Ejecutar(
        Func<long, CancellationToken, Task<Resultado<EstadoJornadaDto>>> operacion,
        CancellationToken ct)
    {
        if (_usuario.UserId is not { } userId) return Unauthorized();

        var resultado = await operacion(userId, ct);
        if (resultado.Ok) return Ok(resultado.Valor);

        return Problem(
            title: resultado.Mensaje,
            statusCode: StatusPara(resultado.Codigo),
            type: resultado.Codigo);
    }

    /// <summary>
    /// Traduce el código de rechazo del dominio al status HTTP que corresponde, para que
    /// el cliente sepa si debe recargar el estado, corregir la entrada o reintentar.
    /// </summary>
    private static int StatusPara(string? codigo) => codigo switch
    {
        CodigosError.IntervaloInvalido => StatusCodes.Status400BadRequest,
        CodigosError.TicketNoEncontrado => StatusCodes.Status404NotFound,
        CodigosError.JornadaNoEncontrada => StatusCodes.Status404NotFound,
        CodigosError.FuenteTicketsNoDisponible => StatusCodes.Status503ServiceUnavailable,

        // El estado real difiere del que suponía el cliente: debe recargar y reintentar.
        CodigosError.AccionNoValida => StatusCodes.Status409Conflict,
        CodigosError.JornadaYaAbierta => StatusCodes.Status409Conflict,
        CodigosError.ConflictoConcurrencia => StatusCodes.Status409Conflict,
        CodigosError.SinSesion => StatusCodes.Status409Conflict,
        CodigosError.SinTareaPrincipal => StatusCodes.Status409Conflict,

        // No es un error: falta una confirmación explícita del usuario.
        CodigosError.ConfirmacionRequerida => StatusCodes.Status428PreconditionRequired,

        _ => StatusCodes.Status400BadRequest,
    };
}
