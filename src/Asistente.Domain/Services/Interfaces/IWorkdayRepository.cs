using Asistente.Domain.Entities;

namespace Asistente.Domain.Services.Interfaces;

/// <summary>
/// Acceso a la jornada. El dominio define el contrato y la capa de persistencia lo
/// implementa, para que las reglas de negocio no dependan de EF Core ni de Oracle.
/// </summary>
public interface IWorkdayRepository
{
    /// <summary>Jornada sin cerrar del usuario, si existe. Como máximo puede haber una (§6.1).</summary>
    Task<Workday?> ObtenerAbiertaAsync(long userId, CancellationToken ct);

    /// <summary>Jornada más reciente del usuario, esté abierta o cerrada.</summary>
    Task<Workday?> ObtenerVigenteAsync(long userId, CancellationToken ct);

    Task AgregarAsync(Workday jornada, CancellationToken ct);

    /// <summary>
    /// Confirma la transición completa en una única transacción (NFR-002).
    /// Devuelve false si otro proceso modificó la jornada mientras tanto.
    /// </summary>
    Task<bool> GuardarAsync(CancellationToken ct);
}
