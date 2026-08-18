using Asistente.Api.Security;
using Asistente.Domain.Dtos;
using Asistente.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Api.Controllers;

/// <summary>
/// Consulta de la base corporativa de tickets. SOLO LECTURA (FR-015): este controlador no
/// expone ninguna operación de escritura, y tampoco podría, porque
/// <see cref="ITicketQueryService"/> no la define.
///
/// Todas las consultas se acotan al usuario autenticado. El filtro lo pone el servidor a
/// partir de la identidad de la sesión y nunca llega como parámetro de la petición: si el
/// cliente pudiera elegirlo, cualquiera vería los tickets de cualquiera.
/// </summary>
[ApiController]
[Route("api/tickets")]
[Microsoft.AspNetCore.Authorization.Authorize]
[Produces("application/json")]
public sealed class TicketsController : ControllerBase
{
    private readonly ITicketQueryService _tickets;
    private readonly IUsuarioActual _usuario;

    public TicketsController(ITicketQueryService tickets, IUsuarioActual usuario)
    {
        _tickets = tickets;
        _usuario = usuario;
    }

    /// <summary>Clientes con los que trabaja el usuario, según sus propios tickets.</summary>
    [HttpGet("clientes")]
    [ProducesResponseType<IReadOnlyList<ClienteDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Clientes(
        [FromQuery] string? q,
        [FromQuery] int maximo = 50,
        CancellationToken ct = default)
    {
        if (_usuario.Actual is not { } usuario) return Unauthorized();

        return Ok(await _tickets.BuscarClientesAsync(
            usuario.Usuario, q, Math.Clamp(maximo, 1, 200), ct));
    }

    /// <summary>
    /// Tickets del usuario, ordenados por fecha de creación descendente (FR-011, AC-10),
    /// con paginación obligatoria para no castigar la base corporativa (FR-014).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PaginaDto<TicketDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Buscar(
        [FromQuery] string? clienteId,
        [FromQuery] string? q,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 8,
        CancellationToken ct = default)
    {
        if (_usuario.Actual is not { } usuario) return Unauthorized();

        var consulta = new ConsultaTickets
        {
            ClienteId = string.IsNullOrWhiteSpace(clienteId) ? null : clienteId,
            Texto = q,
            Pagina = pagina,
            Tamano = tamano,
        };

        return Ok(await _tickets.BuscarTicketsAsync(usuario.Usuario, consulta, ct));
    }

    /// <summary>
    /// Un ticket del usuario por su identificador. Devuelve 404 tanto si no existe como si
    /// pertenece a otra persona: distinguir ambos casos revelaría qué tickets hay.
    /// </summary>
    [HttpGet("{ticketId}")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PorId(string ticketId, CancellationToken ct)
    {
        if (_usuario.Actual is not { } usuario) return Unauthorized();

        var ticket = await _tickets.ObtenerPorIdAsync(usuario.Usuario, ticketId, ct);
        return ticket is null ? NotFound() : Ok(ticket);
    }
}
