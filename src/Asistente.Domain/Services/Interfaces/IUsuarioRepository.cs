using Asistente.Domain.Entities;

namespace Asistente.Domain.Services.Interfaces;

/// <summary>Acceso a usuarios y a sus tokens de un solo uso.</summary>
public interface IUsuarioRepository
{
    Task<AppUser?> BuscarPorIdAsync(long id, CancellationToken ct);
    Task<AppUser?> BuscarPorUsuarioAsync(string usuario, CancellationToken ct);
    Task<AppUser?> BuscarPorEmailAsync(string email, CancellationToken ct);
    Task<bool> ExisteUsuarioAsync(string usuario, CancellationToken ct);

    Task AgregarAsync(AppUser usuario, CancellationToken ct);

    Task AgregarTokenAsync(TokenUsuario token, CancellationToken ct);

    /// <summary>Busca por el HASH del token; el valor en claro nunca llega a la base.</summary>
    Task<TokenUsuario?> BuscarTokenAsync(string tokenHash, TipoToken tipo, CancellationToken ct);

    /// <summary>Invalida los tokens vigentes del mismo tipo, para que solo sirva el último.</summary>
    Task AnularTokensPendientesAsync(long userId, TipoToken tipo, DateTime ahoraUtc, CancellationToken ct);

    Task GuardarAsync(CancellationToken ct);
}
