using Asistente.Common;
using Asistente.Domain.Dtos;
using Asistente.Domain.Entities;
using Asistente.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Domain.Services;

/// <summary>Parámetros de seguridad de la autenticación.</summary>
public sealed class AuthSettings
{
    public const string SectionName = "AuthSettings";

    public int TopeIntentosFallidos { get; set; } = 5;
    public int MinutosBloqueo { get; set; } = 15;

    /// <summary>Ventana del enlace de activación. Holgada: puede tardar en leerse el correo.</summary>
    public int HorasValidezVerificacion { get; set; } = 24;

    /// <summary>Ventana del enlace de restablecimiento. Corta: es la operación más sensible.</summary>
    public int MinutosValidezRestablecer { get; set; } = 60;

    /// <summary>Base para armar los enlaces que van en el correo.</summary>
    public string UrlBaseFrontend { get; set; } = "http://localhost:5173";

    /// <summary>
    /// Devuelve los enlaces en la respuesta HTTP en lugar de exigir leer el correo.
    /// SOLO desarrollo: en cualquier otro entorno permitiría activar cuentas ajenas.
    /// </summary>
    public bool ExponerEnlacesEnRespuesta { get; set; }
}

/// <summary>
/// Registro, inicio de sesión, verificación de correo y restablecimiento de contraseña.
///
/// Dos principios recorren toda la clase:
///
/// 1. No revelar qué cuentas existen. El login responde igual ante usuario inexistente y
///    contraseña equivocada, y el pedido de restablecimiento responde igual siempre.
/// 2. No dejar atajos temporales. Ante un usuario inexistente igual se calcula un hash,
///    para que el tiempo de respuesta no delate la diferencia.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IHasherClave _hasher;
    private readonly IGeneradorTokens _tokens;
    private readonly IServicioCorreo _correo;
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
        IGeneradorTokens tokens,
        IServicioCorreo correo,
        IRelojCorporativo reloj,
        AuthSettings config,
        ILogger<AuthService> logger)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _tokens = tokens;
        _correo = correo;
        _reloj = reloj;
        _config = config;
        _logger = logger;

        _hashSeñuelo = hasher.Hashear("señuelo-para-igualar-tiempos");
    }

    // ---------- Registro ----------

    public async Task<Resultado<RegistroResultado>> RegistrarAsync(
        RegistroRequest request, CancellationToken ct)
    {
        // El usuario debe estar en la lista habilitada. La aplicación es interna: nadie de
        // afuera puede crearse una cuenta.
        var habilitado = UsuariosHabilitados.Buscar(request.Usuario);
        if (habilitado is null)
        {
            return Resultado<RegistroResultado>.Fallo(
                CodigosError.UsuarioNoHabilitado,
                "El nombre de usuario no está habilitado para registrarse.");
        }

        var local = (request.EmailLocal ?? string.Empty).Trim();
        if (local.Length == 0 || local.Contains('@') || local.Any(char.IsWhiteSpace))
        {
            return Resultado<RegistroResultado>.Fallo(
                CodigosError.EmailInvalido,
                "Escribí solo la parte del correo anterior a la arroba.");
        }

        if (request.Clave != request.ClaveConfirmacion)
        {
            return Resultado<RegistroResultado>.Fallo(
                CodigosError.ClaveInvalida, "Las contraseñas no coinciden.");
        }

        var email = UsuariosHabilitados.ArmarEmail(local);

        var problemas = PoliticaClave.Validar(request.Clave, habilitado.Usuario, email);
        if (problemas.Count > 0)
        {
            return Resultado<RegistroResultado>.Fallo(
                CodigosError.ClaveInvalida, string.Join(" ", problemas));
        }

        var ahora = _reloj.AhoraUtc;

        if (await _usuarios.ExisteUsuarioAsync(habilitado.Usuario, ct))
        {
            // La lista de usuarios habilitados es conocida por el equipo, así que decir
            // que ya está registrado no revela nada que no se sepa, y evita que la persona
            // reintente sin entender por qué falla.
            return Resultado<RegistroResultado>.Fallo(
                CodigosError.UsuarioYaRegistrado,
                "Ese usuario ya tiene una cuenta. Probá iniciar sesión o restablecer la contraseña.");
        }

        var usuario = new AppUser(
            habilitado.Usuario, email, habilitado.NombreCompleto, _hasher.Hashear(request.Clave), ahora);

        await _usuarios.AgregarAsync(usuario, ct);
        await _usuarios.GuardarAsync(ct);

        var enlace = await EmitirTokenAsync(
            usuario, TipoToken.VerificacionEmail,
            TimeSpan.FromHours(_config.HorasValidezVerificacion),
            "verificar-email", ct);

        await EnviarVerificacionAsync(usuario, enlace, ct);

        _logger.LogInformation("Cuenta creada para {Usuario}, pendiente de verificación.", usuario.Usuario);

        return Resultado<RegistroResultado>.Exito(new RegistroResultado(
            email, RequiereVerificacion: true,
            EnlaceVerificacion: _config.ExponerEnlacesEnRespuesta ? enlace : null));
    }

    // ---------- Inicio de sesión ----------

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
                "La cuenta está bloqueada temporalmente por intentos fallidos. Probá de nuevo en unos minutos.");
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

        if (!usuario.EmailVerificado)
        {
            // Se informa explícitamente porque la persona ya demostró saber la contraseña:
            // no hay nada que ocultarle y necesita saber qué hacer.
            return Resultado<UsuarioActual>.Fallo(
                CodigosError.EmailNoVerificado,
                "Tenés que activar la cuenta desde el enlace que te enviamos por correo.");
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

    private static Resultado<UsuarioActual> CredencialesInvalidas() =>
        Resultado<UsuarioActual>.Fallo(
            CodigosError.CredencialesInvalidas, "Usuario o contraseña incorrectos.");

    // ---------- Verificación de correo ----------

    public async Task<Resultado> VerificarEmailAsync(string token, CancellationToken ct)
    {
        var ahora = _reloj.AhoraUtc;
        var registro = await _usuarios.BuscarTokenAsync(
            _tokens.Hashear(token ?? string.Empty), TipoToken.VerificacionEmail, ct);

        if (registro is null || !registro.EsUtilizable(ahora))
        {
            return Resultado.Fallo(
                CodigosError.TokenInvalido,
                "El enlace no es válido o ya venció. Pedí uno nuevo desde el inicio de sesión.");
        }

        var usuario = await _usuarios.BuscarPorIdAsync(registro.UserId, ct);
        if (usuario is null) return Resultado.Fallo(CodigosError.TokenInvalido, "El enlace no es válido.");

        usuario.ConfirmarEmail(ahora);
        registro.MarcarUsado(ahora);
        await _usuarios.GuardarAsync(ct);

        _logger.LogInformation("Correo verificado para {Usuario}.", usuario.Usuario);
        return Resultado.Exito();
    }

    // ---------- Restablecimiento ----------

    public async Task<Resultado> SolicitarRestablecerAsync(
        OlvidoClaveRequest request, CancellationToken ct)
    {
        var local = (request.EmailLocal ?? string.Empty).Trim();
        var email = UsuariosHabilitados.ArmarEmail(local);
        var usuario = await _usuarios.BuscarPorEmailAsync(email, ct);

        if (usuario is not null && usuario.Activo)
        {
            var enlace = await EmitirTokenAsync(
                usuario, TipoToken.RestablecerClave,
                TimeSpan.FromMinutes(_config.MinutosValidezRestablecer),
                "restablecer-clave", ct);

            await EnviarRestablecerAsync(usuario, enlace, ct);
            _logger.LogInformation("Enlace de restablecimiento emitido para {Usuario}.", usuario.Usuario);
        }
        else
        {
            _logger.LogInformation("Pedido de restablecimiento para un correo sin cuenta.");
        }

        // Misma respuesta exista o no la cuenta: distinguirlas convertiría este endpoint en
        // un verificador de qué correos están registrados.
        return Resultado.Exito();
    }

    public async Task<Resultado> RestablecerClaveAsync(
        RestablecerClaveRequest request, CancellationToken ct)
    {
        var ahora = _reloj.AhoraUtc;
        var registro = await _usuarios.BuscarTokenAsync(
            _tokens.Hashear(request.Token ?? string.Empty), TipoToken.RestablecerClave, ct);

        if (registro is null || !registro.EsUtilizable(ahora))
        {
            return Resultado.Fallo(
                CodigosError.TokenInvalido,
                "El enlace no es válido o ya venció. Pedí uno nuevo.");
        }

        var usuario = await _usuarios.BuscarPorIdAsync(registro.UserId, ct);
        if (usuario is null) return Resultado.Fallo(CodigosError.TokenInvalido, "El enlace no es válido.");

        if (request.Clave != request.ClaveConfirmacion)
        {
            return Resultado.Fallo(CodigosError.ClaveInvalida, "Las contraseñas no coinciden.");
        }

        var problemas = PoliticaClave.Validar(request.Clave, usuario.Usuario, usuario.Email);
        if (problemas.Count > 0)
        {
            return Resultado.Fallo(CodigosError.ClaveInvalida, string.Join(" ", problemas));
        }

        usuario.CambiarClave(_hasher.Hashear(request.Clave), ahora);
        registro.MarcarUsado(ahora);

        // Quien restablece la contraseña demuestra control del correo, así que también se
        // da por verificado. Y se anulan los demás enlaces pendientes: si el pedido lo
        // originó un atacante, su enlace deja de servir.
        usuario.ConfirmarEmail(ahora);
        await _usuarios.AnularTokensPendientesAsync(usuario.Id, TipoToken.RestablecerClave, ahora, ct);

        await _usuarios.GuardarAsync(ct);

        await _correo.EnviarAsync(
            usuario.Email,
            "Tu contraseña fue cambiada",
            Plantilla(
                "Contraseña actualizada",
                $"Hola {usuario.NombreCompleto}, la contraseña de tu cuenta acaba de cambiarse. "
                + "Si no fuiste vos, avisá al área de sistemas cuanto antes.",
                null, null),
            ct);

        _logger.LogInformation("Contraseña restablecida para {Usuario}.", usuario.Usuario);
        return Resultado.Exito();
    }

    public async Task<UsuarioActual?> ObtenerPorIdAsync(long userId, CancellationToken ct)
    {
        var usuario = await _usuarios.BuscarPorIdAsync(userId, ct);
        return usuario is null || !usuario.Activo ? null : new UsuarioActual(usuario.Id, usuario.Usuario);
    }

    // ---------- Apoyo ----------

    private async Task<string> EmitirTokenAsync(
        AppUser usuario, TipoToken tipo, TimeSpan validez, string ruta, CancellationToken ct)
    {
        var ahora = _reloj.AhoraUtc;

        // Se anulan los anteriores del mismo tipo: si alguien pidió varios enlaces, solo
        // el último debe funcionar.
        await _usuarios.AnularTokensPendientesAsync(usuario.Id, tipo, ahora, ct);

        var (token, hash) = _tokens.Generar();
        await _usuarios.AgregarTokenAsync(
            new TokenUsuario(usuario.Id, tipo, hash, ahora, ahora.Add(validez)), ct);
        await _usuarios.GuardarAsync(ct);

        return $"{_config.UrlBaseFrontend.TrimEnd('/')}/{ruta}?token={Uri.EscapeDataString(token)}";
    }

    private Task EnviarVerificacionAsync(AppUser usuario, string enlace, CancellationToken ct) =>
        _correo.EnviarAsync(
            usuario.Email,
            "Activá tu cuenta del Asistente de Registro",
            Plantilla(
                "Activá tu cuenta",
                $"Hola {usuario.NombreCompleto}, creaste una cuenta en el Asistente de Registro de Tareas. "
                + "Confirmá tu correo para poder entrar.",
                enlace,
                $"El enlace vence en {_config.HorasValidezVerificacion} horas y sirve una sola vez."),
            ct);

    private Task EnviarRestablecerAsync(AppUser usuario, string enlace, CancellationToken ct) =>
        _correo.EnviarAsync(
            usuario.Email,
            "Restablecer tu contraseña",
            Plantilla(
                "Restablecer contraseña",
                $"Hola {usuario.NombreCompleto}, pediste cambiar tu contraseña. "
                + "Si no fuiste vos, ignorá este mensaje: tu contraseña actual sigue funcionando.",
                enlace,
                $"El enlace vence en {_config.MinutosValidezRestablecer} minutos y sirve una sola vez."),
            ct);

    private static string Plantilla(string titulo, string cuerpo, string? enlace, string? nota)
    {
        var boton = enlace is null
            ? string.Empty
            : $"""
               <p style="margin:24px 0">
                 <a href="{enlace}"
                    style="background:#c2560a;color:#fff;padding:12px 22px;border-radius:8px;
                           text-decoration:none;font-weight:600;display:inline-block">
                   {titulo}
                 </a>
               </p>
               <p style="font-size:12px;color:#666">
                 Si el botón no funciona, copiá esta dirección:<br>{enlace}
               </p>
               """;

        var pie = nota is null ? string.Empty : $"""<p style="font-size:12px;color:#666">{nota}</p>""";

        return $"""
            <div style="font-family:Segoe UI,Arial,sans-serif;max-width:520px;margin:0 auto;color:#211d1a">
              <h2 style="color:#211d1a">{titulo}</h2>
              <p>{cuerpo}</p>
              {boton}
              {pie}
              <hr style="border:none;border-top:1px solid #eee;margin:24px 0">
              <p style="font-size:12px;color:#888">Asistente de Registro de Tareas</p>
            </div>
            """;
    }
}
