using Asistente.Common;
using Asistente.Domain.Dtos;

namespace Asistente.Domain.Services.Interfaces;

/// <summary>Deriva y verifica hashes de contraseña.</summary>
public interface IHasherClave
{
    string Hashear(string clave);

    /// <summary>
    /// Verifica en tiempo constante. <paramref name="requiereRehash"/> avisa cuando el hash
    /// guardado usa parámetros viejos y conviene regenerarlo con la contraseña que acaba
    /// de escribir el usuario.
    /// </summary>
    bool Verificar(string hashGuardado, string clave, out bool requiereRehash);
}

public interface IAuthService
{
    Task<Resultado<UsuarioActual>> IniciarSesionAsync(LoginRequest request, CancellationToken ct);

    Task<Resultado> CambiarClavePropiaAsync(long userId, CambioClaveRequest request, CancellationToken ct);

    Task<UsuarioActual?> ObtenerPorIdAsync(long userId, CancellationToken ct);
}

/// <summary>
/// Operaciones reservadas sobre cuentas ajenas. Cada una exige un permiso distinto: quien
/// puede levantar un bloqueo no necesariamente debe poder cambiar contraseñas.
/// </summary>
public interface IMantenimientoUsuariosService
{
    Task<IReadOnlyList<UsuarioMantenimientoDto>> ListarAsync(CancellationToken ct);

    /// <summary>Asigna una contraseña nueva. No existe forma de leer la anterior: es un hash.</summary>
    Task<Resultado> ResetClaveAsync(long ejecutorId, long userId, ResetClaveRequest request, CancellationToken ct);

    Task<Resultado> DesbloquearAsync(long ejecutorId, long userId, CancellationToken ct);
}
