using Asistente.Common;
using Asistente.Domain.Dtos;

namespace Asistente.Domain.Services.Interfaces;

/// <summary>
/// Casos de uso de la jornada. Cada método es una transición completa: se confirma
/// entera o no se confirma, sin cortes parciales (§6.1, NFR-002).
/// </summary>
public interface IWorkdayService
{
    Task<EstadoJornadaDto> ObtenerEstadoAsync(UsuarioActual usuario, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> ComenzarDiaAsync(
        UsuarioActual usuario, ComenzarDiaRequest request, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> FinTareaAsync(
        UsuarioActual usuario, FinTareaRequest request, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> RegistrarInterrupcionAsync(
        UsuarioActual usuario, InterrupcionRequest request, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> SalidaDescansoAsync(UsuarioActual usuario, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> RegresoDescansoAsync(UsuarioActual usuario, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> FinDiaAsync(
        UsuarioActual usuario, FinDiaRequest request, CancellationToken ct);

    Task<Resultado<EstadoJornadaDto>> ReabrirAsync(
        UsuarioActual usuario, ReabrirRequest request, CancellationToken ct);
}
