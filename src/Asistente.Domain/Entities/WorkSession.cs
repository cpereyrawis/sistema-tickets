namespace Asistente.Domain.Entities;

/// <summary>
/// Intervalo continuo de trabajo sobre un ticket. Mientras <see cref="FinUtc"/> es null
/// la sesión está abierta, y una jornada admite como máximo una abierta (§6.1).
/// </summary>
public sealed class WorkSession
{
    private WorkSession() { }

    internal WorkSession(
        TicketRef ticket,
        TipoSesion tipo,
        DateTime inicioUtc,
        DateTime? finUtc,
        AccionOrigen accionOrigen)
    {
        Ticket = ticket;
        Tipo = tipo;
        InicioUtc = inicioUtc;
        FinUtc = finUtc;
        AccionOrigen = accionOrigen;
    }

    public long Id { get; private set; }
    public long WorkdayId { get; private set; }

    public TicketRef Ticket { get; private set; } = null!;
    public TipoSesion Tipo { get; private set; }

    public DateTime InicioUtc { get; private set; }
    public DateTime? FinUtc { get; private set; }

    public AccionOrigen AccionOrigen { get; private set; }

    /// <summary>Marca que el tramo fue tocado por una corrección manual (FR-035).</summary>
    public bool Editada { get; private set; }

    public bool EstaAbierta => FinUtc is null;

    public TimeSpan Duracion(DateTime ahoraUtc) => (FinUtc ?? ahoraUtc) - InicioUtc;

    internal void Cerrar(DateTime finUtc)
    {
        // Invariante §6.1: el fin nunca puede ser anterior al inicio.
        if (finUtc < InicioUtc)
        {
            throw new InvalidOperationException(
                $"El fin de la sesión ({finUtc:O}) es anterior a su inicio ({InicioUtc:O}).");
        }

        FinUtc = finUtc;
    }

    internal void MarcarEditada() => Editada = true;
}
