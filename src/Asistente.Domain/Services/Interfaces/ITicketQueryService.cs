using Asistente.Domain.Dtos;

namespace Asistente.Domain.Services.Interfaces;

/// <summary>
/// Consulta de solo lectura sobre la base corporativa de tickets (§8.2).
///
/// Todas las operaciones reciben el nombre de usuario que inició sesión y devuelven
/// únicamente lo asignado a esa persona. El filtro es parte del contrato y no un
/// parámetro opcional: si fuera opcional, olvidarlo expondría los tickets de todos.
///
/// Devuelve DTOs propios y nunca entidades del esquema corporativo (§11.1), y ninguna
/// implementación puede ejecutar INSERT, UPDATE ni DELETE (FR-015).
/// </summary>
public interface ITicketQueryService
{
    Task<IReadOnlyList<ClienteDto>> BuscarClientesAsync(
        string usuario, string? termino, int maximo, CancellationToken ct);

    Task<PaginaDto<TicketDto>> BuscarTicketsAsync(
        string usuario, ConsultaTickets consulta, CancellationToken ct);

    /// <summary>
    /// Un ticket por su identificador, solo si está asignado a ese usuario. Devuelve null
    /// tanto si no existe como si pertenece a otro: quien consulta no debe poder deducir
    /// la existencia de tickets ajenos.
    /// </summary>
    Task<TicketDto?> ObtenerPorIdAsync(string usuario, string ticketId, CancellationToken ct);
}
