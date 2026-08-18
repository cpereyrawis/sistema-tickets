using Asistente.Domain.Services;

namespace Asistente.Api.Security;

/// <summary>
/// Identidad de quien hace la petición.
///
/// Se abstrae porque hoy resuelve contra una cabecera de desarrollo y mañana lo hará
/// contra la cookie de ASP.NET Core emitida tras la autenticación. Los servicios de
/// dominio no deben enterarse de ese cambio.
/// </summary>
public interface IUsuarioActual
{
    /// <summary>Identidad autenticada, o null si la petición no lo está.</summary>
    UsuarioActual? Actual { get; }
}
