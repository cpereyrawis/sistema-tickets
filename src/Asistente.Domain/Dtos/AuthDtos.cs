namespace Asistente.Domain.Dtos;

/// <param name="Usuario">Nombre elegido de la lista habilitada.</param>
/// <param name="EmailLocal">Parte del correo anterior al dominio; el dominio lo fija el servidor.</param>
public sealed record RegistroRequest(
    string Usuario,
    string EmailLocal,
    string Clave,
    string ClaveConfirmacion);

/// <param name="RequiereVerificacion">Siempre true: la cuenta nace sin verificar.</param>
/// <param name="EnlaceVerificacion">
/// Solo se completa en desarrollo, cuando no hay servidor de correo. En cualquier otro
/// entorno es null: devolverlo permitiría activar cuentas ajenas sin acceso al buzón.
/// </param>
public sealed record RegistroResultado(
    string Email,
    bool RequiereVerificacion,
    string? EnlaceVerificacion);

public sealed record LoginRequest(string Usuario, string Clave);

public sealed record OlvidoClaveRequest(string EmailLocal);

public sealed record RestablecerClaveRequest(string Token, string Clave, string ClaveConfirmacion);

/// <summary>Datos de la sesión de usuario que el frontend necesita mostrar.</summary>
public sealed record SesionUsuarioDto(long Id, string Usuario, string NombreCompleto, string Email);

/// <summary>Opción de la lista precargada que se ofrece en el registro.</summary>
public sealed record UsuarioHabilitadoDto(string Usuario, string NombreCompleto);
