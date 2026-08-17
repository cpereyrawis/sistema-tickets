using Asistente.Domain.Dtos;
using Asistente.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Asistente.Api.Controllers;

/// <summary>
/// Consulta de la fuente corporativa de tickets. SOLO LECTURA (FR-015): este controlador
/// no expone ninguna operación de escritura, y tampoco podría hacerlo, porque
/// <see cref="ITicketQueryService"/> no la define.
/// </summary>
[ApiController]
[Route("api/tickets")]
[Produces("application/json")]
public sealed class TicketsController : ControllerBase
{
    private readonly ITicketQueryService _tickets;

    public TicketsController(ITicketQueryService tickets) => _tickets = tickets;

    /// <summary>Búsqueda incremental de clientes por nombre o código.</summary>
    [HttpGet("clientes")]
    [ProducesResponseType<IReadOnlyList<ClienteDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Clientes(
        [FromQuery] string? q,
        [FromQuery] int maximo = 50,
        CancellationToken ct = default) =>
        Ok(await _tickets.BuscarClientesAsync(q, Math.Clamp(maximo, 1, 200), ct));

    /// <summary>
    /// Tickets ordenados por fecha de creación descendente (FR-011, AC-10), con
    /// paginación obligatoria para no castigar la base corporativa (FR-014).
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
        var consulta = new ConsultaTickets
        {
            ClienteId = string.IsNullOrWhiteSpace(clienteId) ? null : clienteId,
            Texto = q,
            Pagina = pagina,
            Tamano = tamano,
        };

        return Ok(await _tickets.BuscarTicketsAsync(consulta, ct));
    }

    /// <summary>Un ticket puntual por su identificador.</summary>
    [HttpGet("{ticketId}")]
    [ProducesResponseType<TicketDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PorId(string ticketId, CancellationToken ct)
    {
        var ticket = await _tickets.ObtenerPorIdAsync(ticketId, ct);
        return ticket is null ? NotFound() : Ok(ticket);
    }
}
