namespace Asistente.Api.Security;

/// <summary>
/// Identidad del usuario que hace la petición.
///
/// Se abstrae porque hoy resuelve contra una cabecera de desarrollo y mañana lo hará
/// contra la cookie de ASP.NET Core emitida tras la autenticación corporativa. Los
/// servicios de dominio no deben enterarse de ese cambio.
/// </summary>
public interface IUsuarioActual
{
    /// <summary>Id del usuario autenticado, o null si la petición no está autenticada.</summary>
    long? UserId { get; }
}
