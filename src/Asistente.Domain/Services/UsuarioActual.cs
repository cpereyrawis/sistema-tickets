namespace Asistente.Domain.Services;

/// <summary>
/// Identidad de quien hace la petición.
///
/// Lleva el id interno y el nombre de usuario juntos porque ambos se necesitan en el
/// mismo flujo: el id identifica la jornada en la base propia, y el nombre es lo único
/// que vincula con el sistema de tickets. Pasarlos como dos parámetros sueltos invita a
/// que alguien los cruce.
/// </summary>
/// <param name="Id">Clave en la base del asistente.</param>
/// <param name="Usuario">Nombre de inicio de sesión, idéntico al del sistema de tickets.</param>
public sealed record UsuarioActual(long Id, string Usuario);
