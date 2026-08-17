namespace Asistente.Domain.Entities;

/// <summary>
/// Datos mínimos del ticket que el asistente conserva al seleccionarlo.
///
/// Es deliberadamente pobre: solo lo necesario para mostrar, registrar y exportar
/// (NFR-008). Además desacopla la jornada de la fuente corporativa, de modo que si esa
/// base cae, los registros ya confirmados siguen siendo legibles (NFR-014).
/// </summary>
public sealed class TicketRef
{
    private TicketRef() { }

    public TicketRef(string ticketId, string clienteId, string clienteNombre, string titulo)
    {
        TicketId = ticketId;
        ClienteId = clienteId;
        ClienteNombre = clienteNombre;
        Titulo = titulo;
    }

    public string TicketId { get; private set; } = string.Empty;
    public string ClienteId { get; private set; } = string.Empty;
    public string ClienteNombre { get; private set; } = string.Empty;
    public string Titulo { get; private set; } = string.Empty;

    /// <summary>
    /// Devuelve una instancia equivalente e independiente.
    ///
    /// Es necesaria porque EF Core mapea este tipo como propiedad de otra entidad, y cada
    /// propietario —la jornada y cada sesión— necesita su propia instancia. Compartir el
    /// mismo objeto entre dos propietarios hace que el rastreador de cambios lo atribuya
    /// al tipo equivocado y falle al guardar.
    /// </summary>
    public TicketRef Copia() => new(TicketId, ClienteId, ClienteNombre, Titulo);
}
