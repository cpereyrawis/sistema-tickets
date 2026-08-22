using Asistente.Common;
using Asistente.Domain.Dtos;
using Asistente.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Domain.Services;

/// <summary>Parámetros de seguridad de la autenticación.</summary>
public sealed class AuthSettings
{
    public const string SectionName = "AuthSettings";

    public int TopeIntentosFallidos { get; set; } = 5;
    public int MinutosBloqueo { get; set; } = 15;
}

/// <summary>
/// Inicio de sesión y cambio de contraseña.
///
/// No hay registro ni recuperación por correo: las cuentas vienen precargadas y quien
/// olvidó su contraseña la pide a alguien con permiso de mantenimiento. Para un sistema
/// interno con nómina conocida eso elimina toda la superficie del circuito por correo
/// —tokens, buzones, enlaces que caducan— sin perder nada que hiciera falta.
///
/// Dos principios sobreviven del diseño anterior:
///
/// 1. No revelar qué cuentas existen: el login responde igual ante usuario inexistente y
///    contraseña equivocada.
/// 2. No dejar atajos temporales: ante un usuario inexistente igual se calcula un hash,
///    para que el tiempo de respuesta no delate la diferencia.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IHasherClave _hasher;
    private readonly IRelojCorporativo _reloj;
    private readonly AuthSettings _config;
    private readonly ILogger<AuthService> _logger;

    /// <summary>
    /// Hash de una contraseña cualquiera, para gastar el mismo tiempo cuando el usuario no
    /// existe. Sin esto, un login fallido rápido delataría que la cuenta no está.
    /// </summary>
    private readonly string _hashSeñuelo;

    public AuthService(
        IUsuarioRepository usuarios,
        IHasherClave hasher,
        IRelojCorporativo reloj,
        AuthSettings config,
        ILogger<AuthService> logger)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _reloj = reloj;
        _config = config;
        _logger = logger;

        _hashSeñuelo = hasher.Hashear("señuelo-para-igualar-tiempos");
    }

    public async Task<Resultado<UsuarioActual>> IniciarSesionAsync(
        LoginRequest request, CancellationToken ct)
    {
        var ahora = _reloj.AhoraUtc;
        var usuario = await _usuarios.BuscarPorUsuarioAsync(request.Usuario ?? string.Empty, ct);

        if (usuario is null)
        {
            // Se verifica igual contra el señuelo para no responder antes que en el caso
            // en que la cuenta sí existe.
            _hasher.Verificar(_hashSeñuelo, request.Clave ?? string.Empty, out _);
            return CredencialesInvalidas();
        }

        if (usuario.EstaBloqueado(ahora))
        {
            return Resultado<UsuarioActual>.Fallo(
                CodigosError.CuentaBloqueada,
                "La cuenta está bloqueada por intentos fallidos. Pedí que te la desbloqueen o esperá unos minutos.");
        }

        var valida = _hasher.Verificar(usuario.ClaveHash, request.Clave ?? string.Empty, out var requiereRehash);

        if (!valida)
        {
            usuario.RegistrarIngresoFallido(
                ahora, _config.TopeIntentosFallidos, TimeSpan.FromMinutes(_config.MinutosBloqueo));
            await _usuarios.GuardarAsync(ct);

            _logger.LogWarning("Intento de acceso fallido para {Usuario}.", usuario.Usuario);
            return CredencialesInvalidas();
        }

        if (!usuario.Activo)
        {
            return Resultado<UsuarioActual>.Fallo(
                CodigosError.CuentaInactiva, "La cuenta está deshabilitada.");
        }

        // Si el hash quedó con parámetros viejos, este es el único momento en que se tiene
        // la contraseña en claro para regenerarlo.
        if (requiereRehash)
        {
            usuario.ActualizarHash(_hasher.Hashear(request.Clave!));
        }

        usuario.RegistrarIngresoExitoso(ahora);
        await _usuarios.GuardarAsync(ct);

        return Resultado<UsuarioActual>.Exito(new UsuarioActual(usuario.Id, usuario.Usuario));
    }

    /// <summary>
    /// Cambio de la propia contraseña. Exige la actual aunque la sesión ya esté abierta:
    /// una sesión olvidada en una máquina ajena no debe poder quedarse con la cuenta.
    /// </summary>
    public async Task<Resultado> CambiarClavePropiaAsync(
        long userId, CambioClaveRequest request, CancellationToken ct)
    {
        var usuario = await _usuarios.BuscarPorIdAsync(userId, ct);
        if (usuario is null) return Resultado.Fallo(CodigosError.NoAutenticado, "Sesión no válida.");

        if (!_hasher.Verificar(usuario.ClaveHash, request.ClaveActual ?? string.Empty, out _))
        {
            return Resultado.Fallo(
                CodigosError.CredencialesInvalidas, "La contraseña actual no es correcta.");
        }

        if (request.ClaveNueva != request.ClaveConfirmacion)
        {
            return Resultado.Fallo(CodigosError.ClaveInvalida, "Las contraseñas no coinciden.");
        }

        var problemas = PoliticaClave.Validar(request.ClaveNueva);
        if (problemas.Count > 0)
        {
            return Resultado.Fallo(CodigosError.ClaveInvalida, string.Join(" ", problemas));
        }

        usuario.CambiarClave(_hasher.Hashear(request.ClaveNueva!), _reloj.AhoraUtc);
        await _usuarios.GuardarAsync(ct);

        _logger.LogInformation("Contraseña cambiada por el propio usuario {Usuario}.", usuario.Usuario);
        return Resultado.Exito();
    }

    public async Task<UsuarioActual?> ObtenerPorIdAsync(long userId, CancellationToken ct)
    {
        var usuario = await _usuarios.BuscarPorIdAsync(userId, ct);
        return usuario is null ? null : new UsuarioActual(usuario.Id, usuario.Usuario);
    }

    private static Resultado<UsuarioActual> CredencialesInvalidas() =>
        Resultado<UsuarioActual>.Fallo(
            CodigosError.CredencialesInvalidas, "Usuario o contraseña incorrectos.");
}
