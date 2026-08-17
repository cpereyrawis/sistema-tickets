namespace Asistente.Domain.Entities;

/// <summary>
/// Hecho atómico de inicio o fin. Es append-only: las sesiones son la vista cómoda,
/// pero los eventos son la bitácora que permite auditar cómo se llegó a ellas.
/// </summary>
public sealed class TimeEvent
{
    private TimeEvent() { }

    internal TimeEvent(TipoEvento tipo, string ticketId, DateTime ocurridoEnUtc, Guid correlationId)
    {
        Tipo = tipo;
        TicketId = ticketId;
        OcurridoEnUtc = ocurridoEnUtc;
        CorrelationId = correlationId;
        CreadoEnUtc = DateTime.UtcNow;
    }

    public long Id { get; private set; }
    public long WorkdayId { get; private set; }

    public TipoEvento Tipo { get; private set; }
    public string TicketId { get; private set; } = string.Empty;
    public DateTime OcurridoEnUtc { get; private set; }

    /// <summary>
    /// Comparten CorrelationId los eventos generados por una misma transición compuesta.
    /// Los cuatro de una interrupción llevan el mismo (§13.1).
    /// </summary>
    public Guid CorrelationId { get; private set; }

    public DateTime CreadoEnUtc { get; private set; }
}
