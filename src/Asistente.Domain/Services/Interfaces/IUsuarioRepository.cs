using Asistente.Domain.Entities;

namespace Asistente.Domain.Services.Interfaces;

/// <summary>Acceso a usuarios y a sus permisos.</summary>
public interface IUsuarioRepository
{
    Task<AppUser?> BuscarPorIdAsync(long id, CancellationToken ct);
    Task<AppUser?> BuscarPorUsuarioAsync(string usuario, CancellationToken ct);

    Task<IReadOnlyList<AppUser>> ListarTodosAsync(CancellationToken ct);

    /// <summary>Códigos de permiso otorgados a un usuario. Vacío si no tiene ninguno.</summary>
    Task<IReadOnlyList<string>> ListarPermisosAsync(long userId, CancellationToken ct);

    /// <summary>Permisos de varios usuarios de una sola consulta, para armar el listado.</summary>
    Task<IReadOnlyDictionary<long, IReadOnlyList<string>>> ListarPermisosDeTodosAsync(CancellationToken ct);

    Task GuardarAsync(CancellationToken ct);
}
