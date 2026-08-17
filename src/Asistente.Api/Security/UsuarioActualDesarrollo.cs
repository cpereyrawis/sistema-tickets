namespace Asistente.Api.Security;

/// <summary>
/// Identidad de DESARROLLO. Toma el usuario de la cabecera <c>X-Usuario-Id</c> y, si no
/// viene, usa 1.
///
/// No es un mecanismo de autenticación: cualquiera puede declarar ser cualquier usuario.
/// Existe para poder ejercitar los endpoints mientras se define el mecanismo corporativo
/// (decisión D-3 del plan). Está registrada solo en el entorno de desarrollo, y arrancar
/// en Producción con esta implementación debe ser imposible.
/// </summary>
public sealed class UsuarioActualDesarrollo : IUsuarioActual
{
    public const string CabeceraUsuario = "X-Usuario-Id";

    private readonly IHttpContextAccessor _accessor;

    public UsuarioActualDesarrollo(IHttpContextAccessor accessor) => _accessor = accessor;

    public long? UserId
    {
        get
        {
            var valor = _accessor.HttpContext?.Request.Headers[CabeceraUsuario].FirstOrDefault();
            return long.TryParse(valor, out var id) ? id : 1L;
        }
    }
}
