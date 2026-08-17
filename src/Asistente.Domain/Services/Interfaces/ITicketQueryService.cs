using Asistente.Domain.Dtos;

namespace Asistente.Domain.Services.Interfaces;

/// <summary>
/// Consulta de solo lectura sobre la fuente corporativa de tickets (§8.2).
///
/// Devuelve DTOs propios y nunca entidades del esquema corporativo: es la costura que
/// mantiene al dominio desacoplado de una base que no controlamos y que puede cambiar
/// (§11.1). Ninguna implementación puede ejecutar INSERT, UPDATE ni DELETE (FR-015).
/// </summary>
public interface ITicketQueryService
{
    Task<IReadOnlyList<ClienteDto>> BuscarClientesAsync(
        string? termino, int maximo, CancellationToken ct);

    Task<PaginaDto<TicketDto>> BuscarTicketsAsync(
        ConsultaTickets consulta, CancellationToken ct);

    Task<TicketDto?> ObtenerPorIdAsync(string ticketId, CancellationToken ct);
}
