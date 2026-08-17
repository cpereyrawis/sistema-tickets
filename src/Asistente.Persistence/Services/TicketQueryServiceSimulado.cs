using System.Globalization;
using System.Text;
using Asistente.Domain.Dtos;
using Asistente.Domain.Services.Interfaces;

namespace Asistente.Persistence.Services;

/// <summary>
/// Fuente de tickets EN MEMORIA.
///
/// La vista corporativa todavía no se relevó (fase F0-A del plan de implementación), así
/// que escribir SQL contra ella sería inventar un esquema. Esta implementación permite
/// que el backend funcione de punta a punta mientras tanto.
///
/// Cuando exista la vista autorizada se agrega una implementación con Dapper sobre
/// <c>DatabaseSettings.AdoDataSource</c> y se cambia el registro en la inyección de
/// dependencias. Ningún otro archivo necesita cambiar: ese es el punto de que
/// <see cref="ITicketQueryService"/> exista.
/// </summary>
public sealed class TicketQueryServiceSimulado : ITicketQueryService
{
    private static readonly (string Id, string Nombre, string Codigo)[] Clientes =
    [
        ("CLI-001", "Molinos del Norte S.A.", "MOLNOR"),
        ("CLI-002", "Transporte Andino SRL", "TANDINO"),
        ("CLI-003", "Clínica San Martín", "CSMARTIN"),
        ("CLI-004", "Cooperativa Eléctrica Sur", "COOPSUR"),
        ("CLI-005", "Distribuidora Belgrano", "DBELGRA"),
        ("CLI-006", "Bodega Alto Valle", "BALTOV"),
    ];

    private static readonly TicketDto[] Tickets = ConstruirTickets();

    public Task<IReadOnlyList<ClienteDto>> BuscarClientesAsync(
        string? termino, int maximo, CancellationToken ct)
    {
        var t = Normalizar(termino);

        IReadOnlyList<ClienteDto> resultado = Clientes
            .Where(c => t.Length == 0
                || Normalizar(c.Nombre).Contains(t)
                || Normalizar(c.Codigo).Contains(t))
            .Take(maximo)
            .Select(c => new ClienteDto(c.Id, c.Nombre, c.Codigo))
            .ToList();

        return Task.FromResult(resultado);
    }

    public Task<PaginaDto<TicketDto>> BuscarTicketsAsync(ConsultaTickets consulta, CancellationToken ct)
    {
        var t = Normalizar(consulta.Texto);

        var filtrados = Tickets
            .Where(x => consulta.ClienteId is null || x.ClienteId == consulta.ClienteId)
            .Where(x => t.Length == 0
                || Normalizar(x.TicketId).Contains(t)
                || Normalizar(x.Titulo).Contains(t)
                || Normalizar(x.ClienteNombre).Contains(t))
            // Orden descendente por fecha de creación: FR-011 / AC-10.
            .OrderByDescending(x => x.CreadoEnUtc)
            .ToList();

        var pagina = Math.Max(1, consulta.Pagina);
        var tamano = Math.Clamp(consulta.Tamano, 1, 100);

        var items = filtrados
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToList();

        return Task.FromResult(new PaginaDto<TicketDto>(items, filtrados.Count, pagina, tamano));
    }

    public Task<TicketDto?> ObtenerPorIdAsync(string ticketId, CancellationToken ct) =>
        Task.FromResult(Tickets.FirstOrDefault(
            x => string.Equals(x.TicketId, ticketId, StringComparison.OrdinalIgnoreCase)));

    /// <summary>Compara sin distinguir mayúsculas ni acentos.</summary>
    private static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;

        var descompuesto = valor.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        return string.Concat(descompuesto.Where(
            c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));
    }

    private static TicketDto[] ConstruirTickets()
    {
        var hoy = DateTime.UtcNow.Date;
        DateTime Hace(int dias, int hora, int minuto) => hoy.AddDays(-dias).AddHours(hora).AddMinutes(minuto);

        return
        [
            new("SUP-14892", "CLI-001", "Molinos del Norte S.A.",
                "Error al generar remito de salida en depósito 3",
                "En curso", "Alta", Hace(0, 8, 12), "Cristian Pereyra"),
            new("SUP-14889", "CLI-004", "Cooperativa Eléctrica Sur",
                "Solicitud de alta de usuario para facturación",
                "Abierto", "Media", Hace(0, 7, 45), "Marina López"),
            new("SUP-14885", "CLI-002", "Transporte Andino SRL",
                "La app de choferes no sincroniza viajes desde ayer",
                "En curso", "Alta", Hace(1, 17, 3), "Cristian Pereyra"),
            new("SUP-14881", "CLI-003", "Clínica San Martín",
                "Turnos duplicados al reprogramar desde el portal",
                "Pendiente cliente", "Media", Hace(1, 14, 20), "Javier Domínguez"),
            new("SUP-14877", "CLI-001", "Molinos del Norte S.A.",
                "Reporte mensual de stock arroja totales negativos",
                "Abierto", "Alta", Hace(1, 11, 50), "Cristian Pereyra"),
            new("SUP-14870", "CLI-005", "Distribuidora Belgrano",
                "Capacitación de uso del módulo de cobranzas",
                "Abierto", "Baja", Hace(2, 9, 30), "Marina López"),
            new("SUP-14866", "CLI-004", "Cooperativa Eléctrica Sur",
                "Lentitud al consultar histórico de consumos",
                "En curso", "Media", Hace(2, 16, 5), "Cristian Pereyra"),
            new("SUP-14858", "CLI-002", "Transporte Andino SRL",
                "Ajuste de permisos para perfil supervisor de flota",
                "Resuelto", "Baja", Hace(3, 10, 15), "Javier Domínguez"),
            new("SUP-14851", "CLI-006", "Bodega Alto Valle",
                "Integración con balanza no registra pesadas parciales",
                "En curso", "Alta", Hace(3, 8, 40), "Cristian Pereyra"),
            new("SUP-14845", "CLI-003", "Clínica San Martín",
                "Certificado vencido en el servidor de historias clínicas",
                "Cerrado", "Alta", Hace(4, 13, 25), "Marina López"),
            new("SUP-14839", "CLI-005", "Distribuidora Belgrano",
                "Exportación de cuenta corriente sin columna de saldo",
                "Abierto", "Media", Hace(4, 9, 5), "Cristian Pereyra"),
            new("SUP-14830", "CLI-001", "Molinos del Norte S.A.",
                "Backup nocturno finaliza con advertencias",
                "Pendiente cliente", "Media", Hace(5, 22, 10), "Javier Domínguez"),
            new("SUP-14822", "CLI-006", "Bodega Alto Valle",
                "Alta de nueva sucursal en el maestro de depósitos",
                "Resuelto", "Baja", Hace(6, 11, 0), "Cristian Pereyra"),
            new("SUP-14814", "CLI-002", "Transporte Andino SRL",
                "Impresora fiscal no responde en terminal 2",
                "Cerrado", "Alta", Hace(7, 15, 45), "Marina López"),
            new("SUP-14803", "CLI-004", "Cooperativa Eléctrica Sur",
                "Revisión de índices en la base de medidores",
                "En curso", "Media", Hace(8, 10, 30), "Cristian Pereyra"),
        ];
    }
}
