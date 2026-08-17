using Asistente.Domain.Entities;

namespace Asistente.Domain.Dtos;

public sealed record TicketRefDto(string TicketId, string ClienteId, string ClienteNombre, string Titulo);

public sealed record SesionDto(
    long Id,
    TicketRefDto Ticket,
    TipoSesion Tipo,
    DateTime InicioUtc,
    DateTime? FinUtc,
    AccionOrigen AccionOrigen,
    bool Editada);

public sealed record AuditoriaDto(string Accion, DateTime OcurridoEnUtc, string Detalle);

/// <summary>
/// Estado vigente de la jornada.
///
/// Es la ÚNICA respuesta de todas las transiciones y también de la consulta de estado:
/// el frontend nunca infiere en qué estado quedó, lo recibe. Eso elimina de raíz la clase
/// de errores donde la interfaz y el servidor discrepan (§6, FR-027).
/// </summary>
public sealed record EstadoJornadaDto(
    long? JornadaId,
    EstadoJornada Estado,
    DateOnly? FechaLocal,
    DateTime? InicioUtc,
    DateTime? FinUtc,
    TicketRefDto? TicketPrincipal,
    SesionDto? SesionAbierta,
    IReadOnlyList<SesionDto> Sesiones,
    IReadOnlyList<AuditoriaDto> Auditoria,
    /// <summary>Eventos de la bitácora. Se envía el conteo, no la lista: la interfaz solo lo muestra como dato.</summary>
    int CantidadEventos,
    IReadOnlyList<TipoAccion> AccionesHabilitadas,
    IReadOnlyList<TipoAccion> AccionesCorreccion,
    long Version);

// ---------- Peticiones ----------

public sealed record ComenzarDiaRequest(string TicketId);

public sealed record FinTareaRequest(string TicketId);

public sealed record InterrupcionRequest(string TicketId, DateTime InicioUtc, int DuracionMinutos);

public sealed record FinDiaRequest(bool ConfirmadoEnDescanso = false);

public sealed record ReabrirRequest(string Motivo, bool ImputarIntervalo);
