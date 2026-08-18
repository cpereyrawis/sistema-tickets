using System.Security.Cryptography;
using Asistente.Domain.Entities;
using Asistente.Domain.Services.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace Asistente.Persistence.Services;

/// <summary>
/// Deriva contraseñas con <see cref="PasswordHasher{TUser}"/> de ASP.NET Core Identity.
///
/// Se usa el componente del framework en lugar de escribir la derivación a mano por tres
/// razones: aplica PBKDF2-HMAC-SHA512 con una cantidad de iteraciones que Microsoft
/// actualiza con cada versión, embebe la sal y la versión del formato dentro del propio
/// hash —lo que permite migrar de algoritmo sin invalidar las contraseñas existentes— y
/// compara en tiempo constante. Implementar esto uno mismo es la clase de código donde un
/// error no se nota hasta que es tarde.
/// </summary>
public sealed class HasherClave : IHasherClave
{
    private readonly PasswordHasher<AppUser> _interno = new();

    // El hasher del framework necesita una instancia de usuario, pero no la usa para
    // derivar: la sal es aleatoria por hash. Se pasa una fija para no construir objetos.
    private static readonly AppUser Ficticio = new("x", "x@x", "x", "x", DateTime.UnixEpoch);

    public string Hashear(string clave) => _interno.HashPassword(Ficticio, clave);

    public bool Verificar(string hashGuardado, string clave, out bool requiereRehash)
    {
        var resultado = _interno.VerifyHashedPassword(Ficticio, hashGuardado, clave);

        requiereRehash = resultado == PasswordVerificationResult.SuccessRehashNeeded;
        return resultado != PasswordVerificationResult.Failed;
    }
}

/// <summary>
/// Genera los tokens que viajan por correo.
///
/// Son 256 bits del generador criptográfico del sistema, codificados en Base64 apto para
/// URL. Con esa entropía, adivinar uno es inviable, así que para guardarlos alcanza
/// SHA-256: el costo alto de PBKDF2 protege secretos que una persona podría elegir mal,
/// no cadenas aleatorias.
///
/// La comparación posterior se hace buscando por hash en la base, no comparando en
/// memoria, así que no hay superficie para un ataque de tiempo.
/// </summary>
public sealed class GeneradorTokens : IGeneradorTokens
{
    private const int BytesToken = 32;

    public (string Token, string Hash) Generar()
    {
        var bytes = RandomNumberGenerator.GetBytes(BytesToken);
        var token = Base64UrlEncode(bytes);
        return (token, Hashear(token));
    }

    public string Hashear(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    /// <summary>Base64 sin caracteres que haya que escapar en una URL.</summary>
    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
