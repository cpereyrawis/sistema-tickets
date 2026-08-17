namespace Asistente.Domain.Entities;

/// <summary>
/// Usuario del asistente, vinculado a la identidad corporativa por
/// <see cref="ExternalUserId"/>.
///
/// No guarda contraseñas ni hashes: la validación de credenciales ocurre contra el
/// mecanismo corporativo, y acá solo queda la identidad ya verificada (FR-003, AC-16).
/// </summary>
public sealed class AppUser
{
    private AppUser() { }

    public AppUser(string externalUserId, string usuario, string displayName)
    {
        ExternalUserId = externalUserId;
        Usuario = usuario;
        DisplayName = displayName;
        IsActive = true;
    }

    public long Id { get; private set; }

    /// <summary>Identificador estable en el sistema corporativo.</summary>
    public string ExternalUserId { get; private set; } = string.Empty;

    public string Usuario { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime? LastLoginAtUtc { get; private set; }

    public void RegistrarIngreso(DateTime ahoraUtc) => LastLoginAtUtc = ahoraUtc;
}
