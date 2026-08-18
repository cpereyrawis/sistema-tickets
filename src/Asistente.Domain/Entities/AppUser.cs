namespace Asistente.Domain.Entities;

/// <summary>
/// Usuario del asistente.
///
/// <see cref="Usuario"/> es la CLAVE DE VINCULACIÓN con el sistema de tickets: tiene que
/// ser idéntico al de allá, porque es lo que permite listar los tickets asignados a quien
/// inició sesión.
///
/// La contraseña se guarda solo como hash derivado con una función lenta, nunca en claro
/// ni de forma reversible (FR-003, AC-16).
/// </summary>
public sealed class AppUser
{
    private AppUser() { }

    public AppUser(string usuario, string email, string nombreCompleto, string claveHash, DateTime ahoraUtc)
    {
        Usuario = usuario;
        Email = email;
        NombreCompleto = nombreCompleto;
        ClaveHash = claveHash;
        Activo = true;
        EmailVerificado = false;
        FechaAltaUtc = ahoraUtc;
    }

    public long Id { get; private set; }

    /// <summary>Nombre de inicio de sesión, idéntico al del sistema de tickets.</summary>
    public string Usuario { get; private set; } = string.Empty;

    /// <summary>Correo corporativo completo, incluido el dominio.</summary>
    public string Email { get; private set; } = string.Empty;

    public string NombreCompleto { get; private set; } = string.Empty;

    /// <summary>Hash con sal y factor de trabajo embebidos. Nunca la contraseña.</summary>
    public string ClaveHash { get; private set; } = string.Empty;

    public bool Activo { get; private set; }

    /// <summary>
    /// Mientras sea false la cuenta existe pero no puede iniciar sesión. Impide que
    /// alguien registre una cuenta con el correo de otra persona y la use.
    /// </summary>
    public bool EmailVerificado { get; private set; }

    public DateTime FechaAltaUtc { get; private set; }
    public DateTime? EmailVerificadoEnUtc { get; private set; }
    public DateTime? UltimoIngresoUtc { get; private set; }
    public DateTime? UltimoCambioClaveUtc { get; private set; }

    /// <summary>Intentos fallidos consecutivos, para frenar la fuerza bruta (§12.1).</summary>
    public int IntentosFallidos { get; private set; }

    public DateTime? BloqueadoHastaUtc { get; private set; }

    public bool EstaBloqueado(DateTime ahoraUtc) =>
        BloqueadoHastaUtc is not null && BloqueadoHastaUtc > ahoraUtc;

    public bool PuedeIniciarSesion(DateTime ahoraUtc) =>
        Activo && EmailVerificado && !EstaBloqueado(ahoraUtc);

    public void RegistrarIngresoExitoso(DateTime ahoraUtc)
    {
        UltimoIngresoUtc = ahoraUtc;
        IntentosFallidos = 0;
        BloqueadoHastaUtc = null;
    }

    /// <summary>
    /// Cuenta un intento fallido y bloquea temporalmente al llegar al tope. El bloqueo es
    /// por tiempo y no permanente: uno definitivo convertiría cualquier ataque en una
    /// denegación de servicio contra el usuario legítimo.
    /// </summary>
    public void RegistrarIngresoFallido(DateTime ahoraUtc, int topeIntentos, TimeSpan bloqueo)
    {
        IntentosFallidos += 1;

        if (IntentosFallidos >= topeIntentos)
        {
            BloqueadoHastaUtc = ahoraUtc.Add(bloqueo);
            IntentosFallidos = 0;
        }
    }

    public void ConfirmarEmail(DateTime ahoraUtc)
    {
        EmailVerificado = true;
        EmailVerificadoEnUtc = ahoraUtc;
    }

    /// <summary>
    /// Cambia la contraseña y levanta cualquier bloqueo: quien acaba de demostrar control
    /// del correo no debe quedar encerrado por los intentos fallidos del atacante.
    /// </summary>
    public void CambiarClave(string nuevoHash, DateTime ahoraUtc)
    {
        ClaveHash = nuevoHash;
        UltimoCambioClaveUtc = ahoraUtc;
        IntentosFallidos = 0;
        BloqueadoHastaUtc = null;
    }

    /// <summary>Reemplaza el hash cuando el algoritmo quedó desactualizado.</summary>
    public void ActualizarHash(string nuevoHash) => ClaveHash = nuevoHash;
}
