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

    public Task<AppUser?> BuscarPorEmailAsync(string email, CancellationToken ct) =>
        _db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<bool> ExisteUsuarioAsync(string usuario, CancellationToken ct) =>
        _db.Usuarios.AnyAsync(u => u.Usuario == usuario, ct);

    public async Task AgregarAsync(AppUser usuario, CancellationToken ct) =>
        await _db.Usuarios.AddAsync(usuario, ct);

    public async Task AgregarTokenAsync(TokenUsuario token, CancellationToken ct) =>
        await _db.Tokens.AddAsync(token, ct);

    public Task<TokenUsuario?> BuscarTokenAsync(string tokenHash, TipoToken tipo, CancellationToken ct) =>
        _db.Tokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash && t.Tipo == tipo, ct);

    public async Task AnularTokensPendientesAsync(
        long userId, TipoToken tipo, DateTime ahoraUtc, CancellationToken ct)
    {
        var pendientes = await _db.Tokens
            .Where(t => t.UserId == userId
                        && t.Tipo == tipo
                        && t.UsadoEnUtc == null
                        && t.AnuladoEnUtc == null)
            .ToListAsync(ct);

        foreach (var token in pendientes)
        {
            token.Anular(ahoraUtc);
        }
    }

    public Task GuardarAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
