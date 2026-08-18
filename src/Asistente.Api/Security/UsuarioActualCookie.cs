using System.Security.Claims;
using Asistente.Domain.Services;

namespace Asistente.Api.Security;

/// <summary>
/// Identidad tomada de la cookie de sesión.
///
/// Los claims los emitió el servidor al validar las credenciales y viajan dentro de una
/// cookie cifrada y firmada, así que el cliente no puede alterarlos. Sustituye a la
/// implementación de desarrollo por cabeceras, que permitía declararse cualquier usuario.
/// </summary>
public sealed class UsuarioActualCookie : IUsuarioActual
{
    private readonly IHttpContextAccessor _accessor;

    public UsuarioActualCookie(IHttpContextAccessor accessor) => _accessor = accessor;

    public UsuarioActual? Actual
    {
        get
        {
            var user = _accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true) return null;

            var id = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var nombre = user.FindFirstValue(ClaimTypes.Name);

            if (!long.TryParse(id, out var userId) || string.IsNullOrWhiteSpace(nombre))
            {
                return null;
            }

            return new UsuarioActual(userId, nombre);
        }
    }
}
