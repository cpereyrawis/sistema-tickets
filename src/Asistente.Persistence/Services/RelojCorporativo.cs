using Asistente.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Asistente.Persistence.Services;

public sealed class RelojSettings
{
    public const string SectionName = "RelojSettings";

    /// <summary>
    /// Identificador IANA o de Windows de la zona corporativa. .NET 10 acepta ambos en
    /// Windows y Linux, así que no hace falta duplicar la configuración por sistema.
    /// </summary>
    public string ZonaHoraria { get; set; } = "America/Argentina/Buenos_Aires";
}

/// <summary>
/// Reloj real del sistema. Persiste UTC y solo convierte a la zona corporativa para
/// mostrar y exportar (NFR-012).
/// </summary>
public sealed class RelojCorporativo : IRelojCorporativo
{
    private readonly TimeProvider _tiempo;
    private readonly TimeZoneInfo _zona;

    public RelojCorporativo(
        TimeProvider tiempo,
        IOptions<RelojSettings> opciones,
        ILogger<RelojCorporativo> logger)
    {
        _tiempo = tiempo;

        var id = opciones.Value.ZonaHoraria;
        try
        {
            _zona = TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Caer en UTC en silencio desplazaría todas las horas exportadas sin que nadie
            // se entere, así que al menos queda registrado de forma visible.
            logger.LogError(ex, "Zona horaria '{Zona}' no reconocida. Se usa UTC.", id);
            _zona = TimeZoneInfo.Utc;
        }
    }

    public DateTime AhoraUtc => _tiempo.GetUtcNow().UtcDateTime;

    public DateOnly FechaLocal(DateTime instanteUtc) => DateOnly.FromDateTime(AHoraLocal(instanteUtc));

    public DateTime AHoraLocal(DateTime instanteUtc) =>
        TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(instanteUtc, DateTimeKind.Utc), _zona);
}
