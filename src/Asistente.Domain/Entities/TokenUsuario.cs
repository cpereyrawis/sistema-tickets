namespace Asistente.Domain.Entities;

public enum TipoToken
{
    /// <summary>Activación de la cuenta tras el registro.</summary>
    VerificacionEmail = 0,

    /// <summary>Restablecimiento de contraseña olvidada.</summary>
    RestablecerClave = 1,
}

/// <summary>
/// Token de un solo uso enviado por correo.
///
/// En la base se guarda únicamente el HASH del token, igual que con una contraseña: si
/// alguien lograra leer la tabla, no podría usar los enlaces pendientes. La diferencia con
/// la contraseña es el algoritmo — acá alcanza un hash rápido, porque el token son 256
/// bits aleatorios y no hay diccionario que recorrer; el costo alto de PBKDF2 solo tiene
/// sentido frente a secretos elegidos por personas.
///
/// El token vive poco, sirve una sola vez y queda registrado cuándo se usó.
/// </summary>
public sealed class TokenUsuario
{
    private TokenUsuario() { }

    public TokenUsuario(
        long userId,
        TipoToken tipo,
        string tokenHash,
        DateTime creadoEnUtc,
        DateTime expiraEnUtc)
    {
        UserId = userId;
        Tipo = tipo;
        TokenHash = tokenHash;
        CreadoEnUtc = creadoEnUtc;
        ExpiraEnUtc = expiraEnUtc;
    }

    public long Id { get; private set; }
    public long UserId { get; private set; }
    public TipoToken Tipo { get; private set; }

    /// <summary>Hash del token. El valor en claro solo existe en el correo enviado.</summary>
    public string TokenHash { get; private set; } = string.Empty;

    public DateTime CreadoEnUtc { get; private set; }
    public DateTime ExpiraEnUtc { get; private set; }
    public DateTime? UsadoEnUtc { get; private set; }

    /// <summary>Anulado sin usarse, porque se emitió uno nuevo que lo reemplaza.</summary>
    public DateTime? AnuladoEnUtc { get; private set; }

    public bool EsUtilizable(DateTime ahoraUtc) =>
        UsadoEnUtc is null && AnuladoEnUtc is null && ExpiraEnUtc > ahoraUtc;

    public void MarcarUsado(DateTime ahoraUtc) => UsadoEnUtc = ahoraUtc;

    public void Anular(DateTime ahoraUtc) => AnuladoEnUtc = ahoraUtc;
}
