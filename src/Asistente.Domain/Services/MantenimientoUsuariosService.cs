using Asistente.Common;
using Asistente.Domain.Dtos;
using Asistente.Domain.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Asistente.Domain.Services;

/// <summary>
/// Operaciones de Mantenimiento de Usuarios.
///
/// Las dos que ofrece son deliberadamente distintas. Desbloquear no toca la contraseña:
/// si alguien se equivocó cinco veces al tipear, obligarlo a estrenar contraseña sería
/// castigarlo por un error de dedos. Resetear, en cambio, le asigna una nueva, y de paso
/// levanta el bloqueo, porque quien acaba de recibir una contraseña necesita poder usarla.
///
/// Ninguna permite LEER la contraseña de nadie: lo guardado es un hash y no hay vuelta
/// atrás. Es la propiedad que hace que ni el administrador ni quien consulte la base
/// puedan hacerse pasar por otro sin dejar rastro.
/// </summary>
public sealed class MantenimientoUsuariosService : IMantenimientoUsuariosService
{
    private readonly IUsuarioRepository _usuarios;
    private readonly IHasherClave _hasher;
    private readonly IRelojCorporativo _reloj;
    private readonly ILogger<MantenimientoUsuariosService> _logger;

    public MantenimientoUsuariosService(
        IUsuarioRepository usuarios,
        IHasherClave hasher,
        IRelojCorporativo reloj,
        ILogger<MantenimientoUsuariosService> logger)
    {
        _usuarios = usuarios;
        _hasher = hasher;
        _reloj = reloj;
        _logger = logger;
    }

    public async Task<IReadOnlyList<UsuarioMantenimientoDto>> ListarAsync(CancellationToken ct)
    {
        var ahora = _reloj.AhoraUtc;
        var usuarios = await _usuarios.ListarTodosAsync(ct);

        // Los permisos se traen de una sola consulta y no uno por uno: con la nómina
        // completa en pantalla, lo segundo serían tantas idas a la base como filas.
        var permisos = await _usuarios.ListarPermisosDeTodosAsync(ct);

        return usuarios
            .Select(u => new UsuarioMantenimientoDto(
                u.Id,
                u.Usuario,
                u.NombreCompleto,
                u.Activo,
                u.EstaBloqueado(ahora),
                u.BloqueadoHastaUtc,
                u.IntentosFallidos,
                u.UltimoIngresoUtc,
                u.UltimoCambioClaveUtc,
                permisos.TryGetValue(u.Id, out var p) ? p : []))
            .ToList();
    }

    public async Task<Resultado> ResetClaveAsync(
        long ejecutorId, long userId, ResetClaveRequest request, CancellationToken ct)
    {
        var usuario = await _usuarios.BuscarPorIdAsync(userId, ct);
        if (usuario is null) return Resultado.Fallo(CodigosError.UsuarioNoEncontrado, "El usuario no existe.");

        if (request.ClaveNueva != request.ClaveConfirmacion)
        {
            return Resultado.Fallo(CodigosError.ClaveInvalida, "Las contraseñas no coinciden.");
        }

        var problemas = PoliticaClave.Validar(request.ClaveNueva);
        if (problemas.Count > 0)
        {
            return Resultado.Fallo(CodigosError.ClaveInvalida, string.Join(" ", problemas));
        }

        usuario.CambiarClave(_hasher.Hashear(request.ClaveNueva), _reloj.AhoraUtc);
        await _usuarios.GuardarAsync(ct);

        _logger.LogWarning(
            "Contraseña de {Usuario} reasignada por el usuario {EjecutorId}.", usuario.Usuario, ejecutorId);

        return Resultado.Exito();
    }

    public async Task<Resultado> DesbloquearAsync(long ejecutorId, long userId, CancellationToken ct)
    {
        var usuario = await _usuarios.BuscarPorIdAsync(userId, ct);
        if (usuario is null) return Resultado.Fallo(CodigosError.UsuarioNoEncontrado, "El usuario no existe.");

        usuario.Desbloquear();
        await _usuarios.GuardarAsync(ct);

        _logger.LogWarning(
            "Cuenta de {Usuario} desbloqueada por el usuario {EjecutorId}.", usuario.Usuario, ejecutorId);

        return Resultado.Exito();
    }
}
