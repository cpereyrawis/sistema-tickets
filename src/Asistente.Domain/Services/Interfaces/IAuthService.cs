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

/// <summary>Envío de correo. Se abstrae para poder no mandar nada en desarrollo.</summary>
public interface IServicioCorreo
{
    Task EnviarAsync(string destinatario, string asunto, string cuerpoHtml, CancellationToken ct);
}

/// <summary>Genera y verifica los tokens de un solo uso que viajan por correo.</summary>
public interface IGeneradorTokens
{
    /// <summary>Devuelve el token en claro —que solo viaja en el correo— y su hash.</summary>
    (string Token, string Hash) Generar();

    string Hashear(string token);
}

public interface IAuthService
{
    Task<Resultado<RegistroResultado>> RegistrarAsync(RegistroRequest request, CancellationToken ct);

    Task<Resultado<UsuarioActual>> IniciarSesionAsync(LoginRequest request, CancellationToken ct);

    Task<Resultado> VerificarEmailAsync(string token, CancellationToken ct);

    /// <summary>
    /// Siempre informa éxito, exista o no la cuenta: una respuesta distinta permitiría
    /// averiguar qué correos están registrados.
    /// </summary>
    Task<Resultado> SolicitarRestablecerAsync(OlvidoClaveRequest request, CancellationToken ct);

    Task<Resultado> RestablecerClaveAsync(RestablecerClaveRequest request, CancellationToken ct);

    Task<UsuarioActual?> ObtenerPorIdAsync(long userId, CancellationToken ct);
}
