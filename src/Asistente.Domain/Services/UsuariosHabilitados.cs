namespace Asistente.Domain.Services;

/// <summary>
/// Nombres de usuario que pueden registrarse.
///
/// HARDCODEADO A PROPÓSITO para el MVP. En el sistema final esta lista sale del sistema de
/// tickets: solo puede registrarse quien ya existe allá, porque el nombre de usuario es lo
/// único que vincula ambos sistemas y uno inventado no encontraría ningún ticket.
///
/// Que sea una lista cerrada también cumple una función de seguridad: la aplicación es
/// interna y nadie de afuera debería poder crearse una cuenta.
/// </summary>
public static class UsuariosHabilitados
{
    public const string DominioCorreo = "@wis-software.com";

    public static readonly IReadOnlyList<UsuarioHabilitado> Todos =
    [
        new("cpereyra", "Cristian Pereyra"),
        new("mlopez", "Marina López"),
        new("jdominguez", "Javier Domínguez"),
        new("rgimenez", "Rocío Giménez"),
        new("fsosa", "Federico Sosa"),
        new("amartinez", "Ana Martínez"),
        new("dvarela", "Diego Varela"),
        new("lcastro", "Lucía Castro"),
    ];

    public static UsuarioHabilitado? Buscar(string? usuario) =>
        string.IsNullOrWhiteSpace(usuario)
            ? null
            : Todos.FirstOrDefault(u =>
                string.Equals(u.Usuario, usuario.Trim(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Arma el correo corporativo a partir de la parte local que escribe la persona.</summary>
    public static string ArmarEmail(string parteLocal) =>
        parteLocal.Trim().ToLowerInvariant() + DominioCorreo;
}

public sealed record UsuarioHabilitado(string Usuario, string NombreCompleto);
