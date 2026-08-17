namespace Asistente.Domain.Entities;

/// <summary>
/// Corrección manual sobre la jornada (FR-035, NFR-007). Append-only: no se borra ni se
/// edita, porque el valor de la auditoría está justamente en que no se pueda reescribir.
/// </summary>
public sealed class AuditEntry
{
    private AuditEntry() { }

    internal AuditEntry(string accion, DateTime ocurridoEnUtc, long userId, string detalle)
    {
        Accion = accion;
        OcurridoEnUtc = ocurridoEnUtc;
        UserId = userId;
        Detalle = detalle;
    }

    public long Id { get; private set; }
    public long WorkdayId { get; private set; }

    public string Accion { get; private set; } = string.Empty;
    public DateTime OcurridoEnUtc { get; private set; }
    public long UserId { get; private set; }

    /// <summary>Texto acotado y sin datos sensibles (NFR-011).</summary>
    public string Detalle { get; private set; } = string.Empty;
}
