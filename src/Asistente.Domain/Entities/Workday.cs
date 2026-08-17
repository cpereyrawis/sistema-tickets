using Asistente.Common;

namespace Asistente.Domain.Entities;

/// <summary>
/// Jornada de trabajo: raíz del agregado y única puerta de entrada a sus sesiones,
/// eventos y auditoría.
///
/// Las transiciones son métodos de esta clase, no de un servicio, porque los invariantes
/// de §6.1 solo se pueden garantizar si nadie puede tocar las colecciones por fuera. Las
/// listas se exponen como <see cref="IReadOnlyCollection{T}"/> por ese motivo.
///
/// El backend es la autoridad final: aunque el frontend oculte un botón, cada método
/// vuelve a validar el estado (§6).
/// </summary>
public sealed class Workday
{
    private readonly List<WorkSession> _sesiones = [];
    private readonly List<TimeEvent> _eventos = [];
    private readonly List<AuditEntry> _auditoria = [];

    private Workday() { }

    private Workday(long userId, DateOnly fechaLocal, DateTime inicioUtc, TicketRef ticket)
    {
        UserId = userId;
        FechaLocal = fechaLocal;
        InicioUtc = inicioUtc;
        Estado = EstadoJornada.Activa;
        TicketPrincipal = ticket;
    }

    public long Id { get; private set; }
    public long UserId { get; private set; }

    /// <summary>
    /// Fecha operativa local. Se fija al comenzar y no cambia aunque la jornada cruce
    /// medianoche (decisión D-4 del plan de implementación).
    /// </summary>
    public DateOnly FechaLocal { get; private set; }

    public DateTime InicioUtc { get; private set; }
    public DateTime? FinUtc { get; private set; }

    public EstadoJornada Estado { get; private set; }

    /// <summary>Ticket que ocupa el contexto normal de trabajo; cambia con "fin de tarea".</summary>
    public TicketRef? TicketPrincipal { get; private set; }

    /// <summary>Token de concurrencia optimista. Oracle no tiene rowversion nativo.</summary>
    public long Version { get; private set; }

    public IReadOnlyCollection<WorkSession> Sesiones => _sesiones;
    public IReadOnlyCollection<TimeEvent> Eventos => _eventos;
    public IReadOnlyCollection<AuditEntry> Auditoria => _auditoria;

    public WorkSession? SesionAbierta => _sesiones.FirstOrDefault(s => s.EstaAbierta);

    // ---------- Tabla de transiciones (§6) ----------

    /// <summary>Acciones operativas válidas para un estado.</summary>
    public static IReadOnlyList<TipoAccion> AccionesHabilitadas(EstadoJornada estado) => estado switch
    {
        EstadoJornada.Pendiente => [TipoAccion.ComenzarDia],
        EstadoJornada.Activa =>
        [
            TipoAccion.FinTarea,
            TipoAccion.RegistrarInterrupcion,
            TipoAccion.SalidaDescanso,
            TipoAccion.FinDia,
        ],
        EstadoJornada.EnDescanso => [TipoAccion.RegresoDescanso, TipoAccion.FinDia],
        EstadoJornada.Finalizada => [],
        _ => [],
    };

    /// <summary>
    /// Correcciones autorizadas. Se mantienen separadas de las acciones operativas porque
    /// §6 las admite sobre una jornada cerrada "salvo corrección autorizada", pero no son
    /// parte del flujo normal.
    /// </summary>
    public static IReadOnlyList<TipoAccion> AccionesCorreccion(EstadoJornada estado) =>
        estado == EstadoJornada.Finalizada ? [TipoAccion.ReabrirJornada] : [];

    private Resultado Permite(TipoAccion accion)
    {
        if (AccionesHabilitadas(Estado).Contains(accion) || AccionesCorreccion(Estado).Contains(accion))
        {
            return Resultado.Exito();
        }

        return Resultado.Fallo(
            CodigosError.AccionNoValida,
            $"La acción no es válida en el estado \"{Estado}\".");
    }

    // ---------- Transiciones ----------

    /// <summary>Comenzar el día: crea la jornada y su primera sesión principal (§7.1).</summary>
    public static Workday Comenzar(long userId, TicketRef ticket, DateTime ahoraUtc, DateOnly fechaLocal)
    {
        var jornada = new Workday(userId, fechaLocal, ahoraUtc, ticket);
        var correlacion = Guid.NewGuid();

        jornada._sesiones.Add(
            new WorkSession(ticket.Copia(), TipoSesion.Principal, ahoraUtc, null, AccionOrigen.ComenzarDia));
        jornada._eventos.Add(
            new TimeEvent(TipoEvento.InicioPrincipal, ticket.TicketId, ahoraUtc, correlacion));

        return jornada;
    }

    /// <summary>
    /// Fin de tarea: cierra la sesión vigente e inicia la siguiente en la MISMA marca
    /// temporal, de modo que no quede hueco ni solapamiento (§7.2, AC-03).
    /// </summary>
    public Resultado FinTarea(TicketRef siguiente, DateTime ahoraUtc)
    {
        var permitido = Permite(TipoAccion.FinTarea);
        if (!permitido.Ok) return permitido;

        var actual = SesionAbierta;
        if (actual is null)
        {
            return Resultado.Fallo(CodigosError.SinSesion, "No hay una sesión abierta para cerrar.");
        }

        var correlacion = Guid.NewGuid();

        actual.Cerrar(ahoraUtc);
        _eventos.Add(new TimeEvent(
            TipoEvento.FinPrincipal, actual.Ticket.TicketId, ahoraUtc, correlacion));

        _sesiones.Add(new WorkSession(
            siguiente.Copia(), TipoSesion.Principal, ahoraUtc, null, AccionOrigen.FinTarea));
        _eventos.Add(new TimeEvent(
            TipoEvento.InicioPrincipal, siguiente.TicketId, ahoraUtc, correlacion));

        TicketPrincipal = siguiente;
        return Resultado.Exito();
    }

    /// <summary>
    /// Interrupción: genera exactamente cuatro eventos con un CorrelationId común y deja
    /// la tarea principal segmentada en dos tramos, conservando su identidad (§7.3, AC-05).
    /// </summary>
    public Resultado RegistrarInterrupcion(
        TicketRef ticketInterrupcion,
        DateTime inicioUtc,
        int duracionMinutos,
        DateTime ahoraUtc)
    {
        var permitido = Permite(TipoAccion.RegistrarInterrupcion);
        if (!permitido.Ok) return permitido;

        var actual = SesionAbierta;
        if (actual is null)
        {
            return Resultado.Fallo(CodigosError.SinSesion, "No hay una tarea principal activa.");
        }

        var problema = ValidarInterrupcion(inicioUtc, duracionMinutos, ahoraUtc);
        if (problema is not null)
        {
            return Resultado.Fallo(CodigosError.IntervaloInvalido, problema);
        }

        var finUtc = inicioUtc.AddMinutes(duracionMinutos);
        var principal = actual.Ticket;
        var correlacion = Guid.NewGuid();

        actual.Cerrar(inicioUtc);
        _sesiones.Add(new WorkSession(
            ticketInterrupcion.Copia(), TipoSesion.Interrupcion, inicioUtc, finUtc,
            AccionOrigen.RegistrarInterrupcion));
        _sesiones.Add(new WorkSession(
            principal.Copia(), TipoSesion.Principal, finUtc, null, AccionOrigen.RegistrarInterrupcion));

        _eventos.Add(new TimeEvent(TipoEvento.FinPrincipal, principal.TicketId, inicioUtc, correlacion));
        _eventos.Add(new TimeEvent(TipoEvento.InicioInterrupcion, ticketInterrupcion.TicketId, inicioUtc, correlacion));
        _eventos.Add(new TimeEvent(TipoEvento.FinInterrupcion, ticketInterrupcion.TicketId, finUtc, correlacion));
        _eventos.Add(new TimeEvent(TipoEvento.InicioPrincipal, principal.TicketId, finUtc, correlacion));

        return Resultado.Exito();
    }

    /// <summary>
    /// Las seis reglas de validación de una interrupción (FR-034, §5.4 del plan).
    /// Devuelve null si el intervalo es aceptable.
    /// </summary>
    public string? ValidarInterrupcion(DateTime inicioUtc, int duracionMinutos, DateTime ahoraUtc)
    {
        if (duracionMinutos <= 0)
        {
            return "La duración debe ser mayor a cero.";
        }

        var finUtc = inicioUtc.AddMinutes(duracionMinutos);

        if (inicioUtc < InicioUtc)
        {
            return "La interrupción no puede comenzar antes del inicio de la jornada.";
        }

        if (finUtc > ahoraUtc)
        {
            return "La interrupción no puede terminar en el futuro.";
        }

        var actual = SesionAbierta;
        if (actual is not null && inicioUtc < actual.InicioUtc)
        {
            return "La interrupción no puede comenzar antes del tramo de tarea que corta.";
        }

        var solapada = _sesiones.FirstOrDefault(
            s => s.FinUtc is not null && inicioUtc < s.FinUtc && finUtc > s.InicioUtc);

        if (solapada is not null)
        {
            return $"El intervalo se solapa con una sesión ya registrada ({solapada.Ticket.TicketId}).";
        }

        return null;
    }

    /// <summary>
    /// Salida al descanso: solo cierra la sesión principal. No crea una tarea de descanso
    /// ni imputa tiempo durante ese intervalo (§7.4, AC-07).
    /// </summary>
    public Resultado SalidaDescanso(DateTime ahoraUtc)
    {
        var permitido = Permite(TipoAccion.SalidaDescanso);
        if (!permitido.Ok) return permitido;

        var actual = SesionAbierta;
        if (actual is null)
        {
            return Resultado.Fallo(CodigosError.SinSesion, "No hay una sesión abierta para cerrar.");
        }

        actual.Cerrar(ahoraUtc);
        _eventos.Add(new TimeEvent(
            TipoEvento.FinPrincipal, actual.Ticket.TicketId, ahoraUtc, Guid.NewGuid()));

        Estado = EstadoJornada.EnDescanso;
        return Resultado.Exito();
    }

    /// <summary>Regreso del descanso: reanuda el MISMO ticket principal (§7.5, AC-08).</summary>
    public Resultado RegresoDescanso(DateTime ahoraUtc)
    {
        var permitido = Permite(TipoAccion.RegresoDescanso);
        if (!permitido.Ok) return permitido;

        if (TicketPrincipal is null)
        {
            return Resultado.Fallo(CodigosError.SinTareaPrincipal, "No hay tarea principal para reanudar.");
        }

        _sesiones.Add(new WorkSession(
            TicketPrincipal.Copia(), TipoSesion.Principal, ahoraUtc, null, AccionOrigen.RegresoDescanso));
        _eventos.Add(new TimeEvent(
            TipoEvento.InicioPrincipal, TicketPrincipal.TicketId, ahoraUtc, Guid.NewGuid()));

        Estado = EstadoJornada.Activa;
        return Resultado.Exito();
    }

    /// <summary>
    /// Fin del día. Desde el descanso exige confirmación explícita y no crea una
    /// reanudación artificial; la jornada queda datada al fin del último tramo real,
    /// porque durante el descanso no hubo trabajo (§7.6, decisión D-6 del plan).
    /// </summary>
    public Resultado FinDia(DateTime ahoraUtc, bool confirmadoEnDescanso = false)
    {
        var permitido = Permite(TipoAccion.FinDia);
        if (!permitido.Ok) return permitido;

        if (Estado == EstadoJornada.EnDescanso)
        {
            if (!confirmadoEnDescanso)
            {
                return Resultado.Fallo(
                    CodigosError.ConfirmacionRequerida,
                    "La jornada está en descanso. Confirmá que querés cerrarla sin reanudar la tarea.");
            }

            var ultimoCierre = _sesiones
                .Where(s => s.FinUtc is not null)
                .Select(s => s.FinUtc!.Value)
                .DefaultIfEmpty(InicioUtc)
                .Max();

            FinUtc = ultimoCierre;
            Estado = EstadoJornada.Finalizada;
            return Resultado.Exito();
        }

        var actual = SesionAbierta;
        if (actual is null)
        {
            return Resultado.Fallo(CodigosError.SinSesion, "No hay una sesión abierta para cerrar.");
        }

        actual.Cerrar(ahoraUtc);
        _eventos.Add(new TimeEvent(
            TipoEvento.FinPrincipal, actual.Ticket.TicketId, ahoraUtc, Guid.NewGuid()));

        FinUtc = ahoraUtc;
        Estado = EstadoJornada.Finalizada;
        return Resultado.Exito();
    }

    /// <summary>
    /// Reapertura de una jornada cerrada por error, como corrección auditada.
    ///
    /// Nunca reescribe el tramo ya cerrado: agrega uno nuevo. Lo único que decide quien
    /// corrige es dónde empieza ese tramo. Con <paramref name="imputarIntervalo"/> arranca
    /// en el instante del cierre, computando el intervalo como trabajo; sin él arranca
    /// ahora y el intervalo queda como hueco. El dominio no lo adivina porque solo el
    /// usuario sabe si siguió trabajando (FR-035).
    /// </summary>
    public Resultado Reabrir(DateTime ahoraUtc, long userId, string motivo, bool imputarIntervalo)
    {
        var permitido = Permite(TipoAccion.ReabrirJornada);
        if (!permitido.Ok) return permitido;

        if (TicketPrincipal is null)
        {
            return Resultado.Fallo(
                CodigosError.SinTareaPrincipal,
                "La jornada no tiene una tarea principal para reanudar.");
        }

        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Resultado.Fallo(
                CodigosError.IntervaloInvalido,
                "La corrección necesita un motivo.");
        }

        var cerradaEn = FinUtc;
        var imputa = imputarIntervalo && cerradaEn is not null;
        var inicioTramo = imputa ? cerradaEn!.Value : ahoraUtc;

        _sesiones.Add(new WorkSession(
            TicketPrincipal.Copia(), TipoSesion.Principal, inicioTramo, null, AccionOrigen.ReabrirJornada));
        _eventos.Add(new TimeEvent(
            TipoEvento.InicioPrincipal, TicketPrincipal.TicketId, inicioTramo, Guid.NewGuid()));

        var detalle = cerradaEn is null
            ? $"Motivo: {motivo}. Se reanudó {TicketPrincipal.TicketId}."
            : $"Cerrada a las {cerradaEn:HH:mm} UTC. Motivo: {motivo}. Se reanudó "
              + $"{TicketPrincipal.TicketId} "
              + (imputa
                  ? "imputando el intervalo como trabajo sobre esa tarea."
                  : "sin imputar el intervalo intermedio.");

        _auditoria.Add(new AuditEntry("Reapertura de jornada", ahoraUtc, userId, detalle));

        FinUtc = null;
        Estado = EstadoJornada.Activa;
        return Resultado.Exito();
    }
}
