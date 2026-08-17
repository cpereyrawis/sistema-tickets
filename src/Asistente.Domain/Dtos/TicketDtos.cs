namespace Asistente.Domain.Dtos;

public sealed record ClienteDto(string Id, string Nombre, string Codigo);

public sealed record TicketDto(
    string TicketId,
    string ClienteId,
    string ClienteNombre,
    string Titulo,
    string Estado,
    string Prioridad,
    DateTime CreadoEnUtc,
    string AsignadoA);

/// <summary>Filtros de la consulta de tickets (§8.1).</summary>
public sealed record ConsultaTickets
{
    public string? ClienteId { get; init; }
    public string? Texto { get; init; }
    public int Pagina { get; init; } = 1;
    public int Tamano { get; init; } = 8;
}

public sealed record PaginaDto<T>(IReadOnlyList<T> Items, int Total, int Pagina, int Tamano);
