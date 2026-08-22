using Asistente.Domain.Entities;
using Asistente.Domain.Services.Interfaces;
using Asistente.Persistence.Database;
using Microsoft.EntityFrameworkCore;

namespace Asistente.Persistence.Repositories;

public sealed class UsuarioRepository : IUsuarioRepository
{
    private readonly AsistenteDbContext _db;

    public UsuarioRepository(AsistenteDbContext db) => _db = db;

    public Task<AppUser?> BuscarPorIdAsync(long id, CancellationToken ct) =>
        _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, ct);

    // La comparación la resuelve la base según su intercalación, que el script 02 fuerza a
    // ser insensible a mayúsculas. Traducir aquí con ToLower() impediría usar el índice.
    public Task<AppUser?> BuscarPorUsuarioAsync(string usuario, CancellationToken ct) =>
        _db.Usuarios.FirstOrDefaultAsync(u => u.Usuario == usuario, ct);

    public async Task<IReadOnlyList<AppUser>> ListarTodosAsync(CancellationToken ct) =>
        await _db.Usuarios.OrderBy(u => u.Usuario).ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListarPermisosAsync(long userId, CancellationToken ct) =>
        await _db.UsuarioPermisos
            .Where(up => up.UserId == userId)
            .Join(_db.Permisos, up => up.PermisoId, p => p.Id, (_, p) => p.Codigo)
            .ToListAsync(ct);

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<string>>> ListarPermisosDeTodosAsync(
        CancellationToken ct)
    {
        var filas = await _db.UsuarioPermisos
            .Join(_db.Permisos, up => up.PermisoId, p => p.Id, (up, p) => new { up.UserId, p.Codigo })
            .ToListAsync(ct);

        return filas
            .GroupBy(f => f.UserId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(f => f.Codigo).ToList());
    }

    public Task GuardarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
