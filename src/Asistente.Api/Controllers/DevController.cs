using Asistente.Api.Security;
using Asistente.Domain.Dtos;
using Asistente.Domain.Entities;
using Asistente.Domain.Services.Interfaces;
using Asistente.Persistence.Database;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Api.Controllers;

/// <summary>
/// Utilidades para probar la aplicación. SOLO se registran en desarrollo: crear y borrar
/// jornadas ajenas al flujo normal no tiene lugar fuera de una máquina de trabajo.
/// </summary>
[ApiController]
[Route("api/dev")]
[Produces("application/json")]
[SoloDesarrollo]
public sealed class DevController : ControllerBase
{
    private readonly AsistenteDbContext _db;
    private readonly IWorkdayService _servicio;
    private readonly IRelojCorporativo _reloj;
    private readonly IUsuarioActual _usuario;

    public DevController(
        AsistenteDbContext db,
        IWorkdayService servicio,
        IRelojCorporativo reloj,
        IUsuarioActual usuario)
    {
        _db = db;
        _servicio = servicio;
        _reloj = reloj;
        _usuario = usuario;
    }

    /// <summary>
    /// Siembra una jornada de ejemplo de cinco horas con cambio de tarea, una interrupción
    /// y un descanso.
    ///
    /// No se arma escribiendo filas a mano: se construye aplicando las propias transiciones
    /// del agregado con marcas temporales explícitas, así el ejemplo cumple los mismos
    /// invariantes que una jornada real y no puede quedar inconsistente con el dominio.
    /// </summary>
    [HttpPost("jornada-ejemplo")]
    [ProducesResponseType<EstadoJornadaDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> JornadaEjemplo(CancellationToken ct)
    {
        if (_usuario.UserId is not { } userId) return Unauthorized();

        await BorrarJornadasAsync(userId, ct);

        var ahora = _reloj.AhoraUtc;
        var t0 = ahora.AddMinutes(-300);

        TicketRef Ticket(string id, string cliId, string cli, string titulo) =>
            new(id, cliId, cli, titulo);

        var jornada = Workday.Comenzar(
            userId,
            Ticket("SUP-14892", "CLI-001", "Molinos del Norte S.A.",
                "Error al generar remito de salida en depósito 3"),
            t0,
            _reloj.FechaLocal(t0));

        jornada.FinTarea(
            Ticket("SUP-14885", "CLI-002", "Transporte Andino SRL",
                "La app de choferes no sincroniza viajes desde ayer"),
            t0.AddMinutes(80));

        jornada.RegistrarInterrupcion(
            Ticket("SUP-14889", "CLI-004", "Cooperativa Eléctrica Sur",
                "Solicitud de alta de usuario para facturación"),
            t0.AddMinutes(120),
            20,
            t0.AddMinutes(145));

        jornada.SalidaDescanso(t0.AddMinutes(190));
        jornada.RegresoDescanso(t0.AddMinutes(225));

        _db.Jornadas.Add(jornada);
        await _db.SaveChangesAsync(ct);

        return Ok(await _servicio.ObtenerEstadoAsync(userId, ct));
    }

    /// <summary>Borra las jornadas del usuario para volver al estado inicial.</summary>
    [HttpDelete("jornada")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reiniciar(CancellationToken ct)
    {
        if (_usuario.UserId is not { } userId) return Unauthorized();

        await BorrarJornadasAsync(userId, ct);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private async Task BorrarJornadasAsync(long userId, CancellationToken ct)
    {
        var previas = await _db.Jornadas.Where(j => j.UserId == userId).ToListAsync(ct);
        _db.Jornadas.RemoveRange(previas);
    }
}
