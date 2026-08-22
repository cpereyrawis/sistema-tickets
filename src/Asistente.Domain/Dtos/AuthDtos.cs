namespace Asistente.Domain.Dtos;

public sealed record LoginRequest(string Usuario, string Clave);

/// <summary>
/// Cambio de la propia contraseña. Pide la actual aunque haya sesión abierta: es lo que
/// impide que una sesión olvidada abierta se convierta en una cuenta perdida.
/// </summary>
public sealed record CambioClaveRequest(string ClaveActual, string ClaveNueva, string ClaveConfirmacion);

/// <summary>
/// Datos de la sesión que el frontend necesita mostrar.
///
/// Incluye los permisos porque la interfaz decide con ellos qué ofrecer —el acceso a
/// Mantenimiento de Usuarios, sin ir más lejos—. Es una comodidad de presentación, no
/// una medida de seguridad: el servidor vuelve a verificar cada permiso en cada
/// operación, porque ocultar un botón no impide llamar al endpoint.
/// </summary>
public sealed record SesionUsuarioDto(
    long Id,
    string Usuario,
    string NombreCompleto,
    IReadOnlyList<string> Permisos);

/// <summary>Fila del listado de Mantenimiento de Usuarios.</summary>
public sealed record UsuarioMantenimientoDto(
    long Id,
    string Usuario,
    string NombreCompleto,
    bool Activo,
    bool Bloqueado,
    DateTime? BloqueadoHastaUtc,
    int IntentosFallidos,
    DateTime? UltimoIngresoUtc,
    DateTime? UltimoCambioClaveUtc,
    IReadOnlyList<string> Permisos);

/// <summary>Contraseña que un administrador le asigna a otro usuario.</summary>
public sealed record ResetClaveRequest(string ClaveNueva, string ClaveConfirmacion);
