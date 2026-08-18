using Asistente.Common;
using Asistente.Domain.Dtos;
using Asistente.Domain.Entities;
using Asistente.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Domain.Services;

/// <summary>
/// Casos de uso de la jornada.
///
/// El servicio orquesta: resuelve el ticket contra la fuente corporativa, delega la regla
/// de negocio en el agregado <see cref="Workday"/> y confirma. No duplica las reglas: si
/// una transición es válida o no lo decide el agregado, que es donde viven los invariantes.
/// </summary>
public sealed class WorkdayService : IWorkdayService
{
    private readonly IWorkdayRepository _repositorio;
    private readonly ITicketQueryService _tickets;
    private readonly IRelojCorporativo _reloj;
    private readonly ILogger<WorkdayService> _logger;

    public WorkdayService(
        IWorkdayRepository repositorio,
        ITicketQueryService tickets,
        IRelojCorporativo reloj,
        ILogger<WorkdayService> logger)
    {
        _repositorio = repositorio;
        _tickets = tickets;
        _reloj = reloj;
        _logger = logger;
    }

    public async Task<EstadoJornadaDto> ObtenerEstadoAsync(UsuarioActual usuario, CancellationToken ct)
    {
        var jornada = await _repositorio.ObtenerVigenteAsync(usuario.Id, ct);
        return Mapear(jornada);
    }

    /// <summary>
    /// Acciones operativas que la interfaz debe ofrecer.
    ///
    /// Sobre una jornada cerrada la tabla de §6 no habilita nada, pero eso solo vale
    /// mientras siga siendo el mismo día: si la jornada cerrada es de una fecha anterior,
    /// lo que corresponde es poder empezar una nueva.
    /// </summary>
    private List<TipoAccion> AccionesPara(Workday jornada)
    {
        if (jornada.Estado != EstadoJornada.Finalizada)
        {
            return Workday.AccionesHabilitadas(jornada.Estado).ToList();
        }

        var hoy = _reloj.FechaLocal(_reloj.AhoraUtc);
        return jornada.FechaLocal < hoy ? [TipoAccion.ComenzarDia] : [];
    }

    public async Task<Resultado<EstadoJornadaDto>> ComenzarDiaAsync(
        UsuarioActual usuario, ComenzarDiaRequest request, CancellationToken ct)
    {
        // Invariante §6.1: un usuario puede tener como máximo una jornada abierta.
        var abierta = await _repositorio.ObtenerAbiertaAsync(usuario.Id, ct);
        if (abierta is not null)
        {
            return Resultado<EstadoJornadaDto>.Fallo(
                CodigosError.JornadaYaAbierta,
                "Ya tenés una jornada abierta. Cerrala antes de comenzar otra.");
        }

        var ticket = await ResolverTicketAsync(usuario.Usuario, request.TicketId, ct);
        if (!ticket.Ok) return Resultado<EstadoJornadaDto>.Fallo(ticket);

        var ahora = _reloj.AhoraUtc;
        var jornada = Workday.Comenzar(usuario.Id, ticket.Valor!, ahora, _reloj.FechaLocal(ahora));

        await _repositorio.AgregarAsync(jornada, ct);
        _logger.LogInformation(
            "Jornada iniciada para el usuario {UserId} con el ticket {TicketId}",
            usuario.Id, request.TicketId);

        return await ConfirmarAsync(jornada, ct);
    }

    public async Task<Resultado<EstadoJornadaDto>> FinTareaAsync(
        UsuarioActual usuario, FinTareaRequest request, CancellationToken ct)
    {
        var jornada = await _repositorio.ObtenerAbiertaAsync(usuario.Id, ct);
        if (jornada is null) return SinJornada();

        var ticket = await ResolverTicketAsync(usuario.Usuario, request.TicketId, ct);
        if (!ticket.Ok) return Resultado<EstadoJornadaDto>.Fallo(ticket);

        var resultado = jornada.FinTarea(ticket.Valor!, _reloj.AhoraUtc);
        if (!resultado.Ok) return Resultado<EstadoJornadaDto>.Fallo(resultado);

        return await ConfirmarAsync(jornada, ct);
    }

    public async Task<Resultado<EstadoJornadaDto>> RegistrarInterrupcionAsync(
        UsuarioActual usuario, InterrupcionRequest request, CancellationToken ct)
    {
        var jornada = await _repositorio.ObtenerAbiertaAsync(usuario.Id, ct);
        if (jornada is null) return SinJornada();

        var ticket = await ResolverTicketAsync(usuario.Usuario, request.TicketId, ct);
        if (!ticket.Ok) return Resultado<EstadoJornadaDto>.Fallo(ticket);

        var resultado = jornada.RegistrarInterrupcion(
            ticket.Valor!, request.InicioUtc, request.DuracionMinutos, _reloj.AhoraUtc);

        if (!resultado.Ok) return Resultado<EstadoJornadaDto>.Fallo(resultado);

        return await ConfirmarAsync(jornada, ct);
    }

    public async Task<Resultado<EstadoJornadaDto>> SalidaDescansoAsync(UsuarioActual usuario, CancellationToken ct)
    {
        var jornada = await _repositorio.ObtenerAbiertaAsync(usuario.Id, ct);
        if (jornada is null) return SinJornada();

        var resultado = jornada.SalidaDescanso(_reloj.AhoraUtc);
        if (!resultado.Ok) return Resultado<EstadoJornadaDto>.Fallo(resultado);

        return await ConfirmarAsync(jornada, ct);
    }

    public async Task<Resultado<EstadoJornadaDto>> RegresoDescansoAsync(UsuarioActual usuario, CancellationToken ct)
    {
        var jornada = await _repositorio.ObtenerAbiertaAsync(usuario.Id, ct);
        if (jornada is null) return SinJornada();

        var resultado = jornada.RegresoDescanso(_reloj.AhoraUtc);
        if (!resultado.Ok) return Resultado<EstadoJornadaDto>.Fallo(resultado);

        return await ConfirmarAsync(jornada, ct);
    }

    public async Task<Resultado<EstadoJornadaDto>> FinDiaAsync(
        UsuarioActual usuario, FinDiaRequest request, CancellationToken ct)
    {
        var jornada = await _repositorio.ObtenerAbiertaAsync(usuario.Id, ct);
        if (jornada is null) return SinJornada();

        var resultado = jornada.FinDia(_reloj.AhoraUtc, request.ConfirmadoEnDescanso);
        if (!resultado.Ok) return Resultado<EstadoJornadaDto>.Fallo(resultado);

        return await ConfirmarAsync(jornada, ct);
    }

    public async Task<Resultado<EstadoJornadaDto>> ReabrirAsync(
        UsuarioActual usuario, ReabrirRequest request, CancellationToken ct)
    {
        // La reapertura opera sobre una jornada ya cerrada, así que se busca la vigente
        // y no la abierta (§6, "salvo corrección autorizada").
        var jornada = await _repositorio.ObtenerVigenteAsync(usuario.Id, ct);
        if (jornada is null) return SinJornada();

        var resultado = jornada.Reabrir(
            _reloj.AhoraUtc, usuario.Id, request.Motivo, request.ImputarIntervalo);

        if (!resultado.Ok) return Resultado<EstadoJornadaDto>.Fallo(resultado);

        _logger.LogWarning(
            "Jornada {JornadaId} reabierta por el usuario {UserId}. Imputa intervalo: {Imputa}",
            jornada.Id, usuario.Id, request.ImputarIntervalo);

        return await ConfirmarAsync(jornada, ct);
    }

    // ---------- Apoyo ----------

    private static Resultado<EstadoJornadaDto> SinJornada() =>
        Resultado<EstadoJornadaDto>.Fallo(
            CodigosError.JornadaNoEncontrada,
            "No hay una jornada sobre la que operar.");

    /// <summary>
    /// Toma una foto mínima del ticket desde la fuente corporativa. Se guarda con la
    /// sesión para que la jornada siga siendo legible aunque esa base no responda (NFR-014).
    /// </summary>
    private async Task<Resultado<TicketRef>> ResolverTicketAsync(
        string nombreUsuario, string ticketId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return Resultado<TicketRef>.Fallo(
                CodigosError.TicketNoEncontrado, "Falta indicar el ticket.");
        }

        var dto = await _tickets.ObtenerPorIdAsync(nombreUsuario, ticketId, ct);
        if (dto is null)
        {
            return Resultado<TicketRef>.Fallo(
                CodigosError.TicketNoEncontrado,
                $"El ticket {ticketId} no existe en la fuente de tickets.");
        }

        return Resultado<TicketRef>.Exito(
            new TicketRef(dto.TicketId, dto.ClienteId, dto.ClienteNombre, dto.Titulo));
    }

    private async Task<Resultado<EstadoJornadaDto>> ConfirmarAsync(Workday jornada, CancellationToken ct)
    {
        var guardado = await _repositorio.GuardarAsync(ct);
        if (!guardado)
        {
            return Resultado<EstadoJornadaDto>.Fallo(
                CodigosError.ConflictoConcurrencia,
                "La jornada cambió mientras preparabas esta operación. Volvé a intentarlo con el estado actualizado.");
        }

        return Resultado<EstadoJornadaDto>.Exito(Mapear(jornada));
    }

    private EstadoJornadaDto Mapear(Workday? jornada)
    {
        if (jornada is null)
        {
            return new EstadoJornadaDto(
                null, EstadoJornada.Pendiente, null, null, null, null, null,
                [], [], 0, Workday.AccionesHabilitadas(EstadoJornada.Pendiente), [], 0);
        }

        var sesiones = jornada.Sesiones
            .OrderBy(s => s.InicioUtc)
            .Select(MapearSesion)
            .ToList();

        return new EstadoJornadaDto(
            jornada.Id,
            jornada.Estado,
            jornada.FechaLocal,
            jornada.InicioUtc,
            jornada.FinUtc,
            jornada.TicketPrincipal is null ? null : MapearTicket(jornada.TicketPrincipal),
            jornada.SesionAbierta is null ? null : MapearSesion(jornada.SesionAbierta),
            sesiones,
            jornada.Auditoria
                .OrderBy(a => a.OcurridoEnUtc)
                .Select(a => new AuditoriaDto(a.Accion, a.OcurridoEnUtc, a.Detalle))
                .ToList(),
            jornada.Eventos.Count,
            AccionesPara(jornada),
            Workday.AccionesCorreccion(jornada.Estado).ToList(),
            jornada.Version);
    }

    private static TicketRefDto MapearTicket(TicketRef t) =>
        new(t.TicketId, t.ClienteId, t.ClienteNombre, t.Titulo);

    private static SesionDto MapearSesion(WorkSession s) =>
        new(s.Id, MapearTicket(s.Ticket), s.Tipo, s.InicioUtc, s.FinUtc, s.AccionOrigen, s.Editada);
}
