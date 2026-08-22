namespace Asistente.Domain.Entities;

/// <summary>
/// Permiso que habilita una operación reservada.
///
/// Es una tabla y no un booleano <c>ES_ADMIN</c> en el usuario a propósito. Un booleano
/// obliga a alterar el esquema cada vez que aparece una atribución nueva, y funde en un
/// solo interruptor cosas que no tienen por qué ir juntas: poder desbloquear una cuenta no
/// implica poder cambiarle la contraseña a otro. Con un catálogo, sumar una atribución es
/// insertar una fila.
///
/// El código, no el id, es lo que mira la aplicación: los ids dependen del orden de
/// inserción y cambiarían entre una base y otra.
/// </summary>
public sealed class Permiso
{
    private Permiso() { }

    public Permiso(string codigo, string descripcion)
    {
        Codigo = codigo;
        Descripcion = descripcion;
    }

    public long Id { get; private set; }

    /// <summary>Identificador estable que usa el código. Ej: <c>USUARIO_RESET_CLAVE</c>.</summary>
    public string Codigo { get; private set; } = string.Empty;

    public string Descripcion { get; private set; } = string.Empty;
}

/// <summary>Otorgamiento de un permiso a un usuario.</summary>
public sealed class UsuarioPermiso
{
    private UsuarioPermiso() { }

    public UsuarioPermiso(long userId, long permisoId, DateTime otorgadoEnUtc)
    {
        UserId = userId;
        PermisoId = permisoId;
        OtorgadoEnUtc = otorgadoEnUtc;
    }

    public long UserId { get; private set; }
    public long PermisoId { get; private set; }
    public DateTime OtorgadoEnUtc { get; private set; }
}

/// <summary>
/// Códigos de permiso conocidos por la aplicación.
///
/// Se declaran como constantes para que un error de tipeo lo detecte el compilador y no
/// una autorización que falla en silencio. Tienen que coincidir con las filas sembradas en
/// <c>T_PERMISO</c>.
/// </summary>
public static class Permisos
{
    /// <summary>Asignar una contraseña nueva a otro usuario.</summary>
    public const string UsuarioResetClave = "USUARIO_RESET_CLAVE";

    /// <summary>Levantar el bloqueo por intentos fallidos de otro usuario.</summary>
    public const string UsuarioDesbloquear = "USUARIO_DESBLOQUEAR";

    /// <summary>Ver el listado de usuarios y su estado.</summary>
    public const string UsuarioListar = "USUARIO_LISTAR";

    public static readonly IReadOnlyList<(string Codigo, string Descripcion)> Todos =
    [
        (UsuarioListar, "Ver el listado de usuarios y su estado"),
        (UsuarioResetClave, "Asignar una contraseña nueva a otro usuario"),
        (UsuarioDesbloquear, "Levantar el bloqueo por intentos fallidos"),
    ];
}
