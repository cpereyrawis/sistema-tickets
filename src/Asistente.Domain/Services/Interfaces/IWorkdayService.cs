using Asistente.Common;
using Asistente.Domain.Dtos;

namespace Asistente.Domain.Services.Interfaces;

/// <summary>
/// Casos de uso de la jornada. Cada método es una transición completa: se confirma
/// entera o no se confirma, sin cortes parciales (§6.1, NFR-002).
/// </summary>
public interface IWorkdayService
{
    Task<EstadoJornadaDto> ObtenerEstadoAsync(long userId, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> ComenzarDiaAsync(
        long userId, ComenzarDiaRequest request, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> FinTareaAsync(
        long userId, FinTareaRequest request, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> RegistrarInterrupcionAsync(
        long userId, InterrupcionRequest request, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> SalidaDescansoAsync(long userId, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> RegresoDescansoAsync(long userId, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> FinDiaAsync(
        long userId, FinDiaRequest request, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> ReabrirAsync(
        long userId, ReabrirRequest request, CancellationToken ct);
}
