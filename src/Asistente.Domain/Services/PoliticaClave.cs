using System.Text.RegularExpressions;

namespace Asistente.Domain.Services;

/// <summary>
/// Reglas de fortaleza de contraseña.
///
/// Combina las dos escuelas a propósito. La longitud mínima de 12 caracteres viene de la
/// recomendación moderna (NIST SP 800-63B), que sostiene que el largo aporta mucho más
/// entropía que obligar a mezclar tipos de carácter. Las reglas de composición se suman
/// porque son las que la gente espera encontrar y porque frenan las contraseñas cortas y
/// obvias que igual cumplirían el largo.
///
/// Se rechaza además lo que ninguna regla de composición detecta: contraseñas frecuentes,
/// secuencias, y todo lo que contenga el propio usuario o correo. Es ahí donde se cae la
/// mayoría de las contraseñas reales.
/// </summary>
public static partial class PoliticaClave
{
    public const int LargoMinimo = 12;
    public const int LargoMaximo = 128;

    /// <summary>
    /// Contraseñas y raíces que aparecen en cualquier lista de filtraciones. No pretende
    /// ser exhaustiva: es la primera barrera contra lo más evidente.
    /// </summary>
    private static readonly string[] Frecuentes =
    [
        "password", "contrasena", "contraseña", "qwerty", "asdf", "zxcv",
        "123456", "1234567890", "abc123", "admin", "administrador", "usuario",
        "bienvenido", "welcome", "letmein", "iloveyou", "monkey", "dragon",
        "wis", "wissoftware", "asistente", "tickets",
    ];

    public static IReadOnlyList<string> Validar(string? clave, string? usuario, string? email)
    {
        var errores = new List<string>();

        if (string.IsNullOrWhiteSpace(clave))
        {
            errores.Add("Ingresá una contraseña.");
            return errores;
        }

        if (clave.Length < LargoMinimo)
        {
            errores.Add($"Debe tener al menos {LargoMinimo} caracteres.");
        }

        if (clave.Length > LargoMaximo)
        {
            // El tope evita que una entrada enorme dispare un cómputo costoso de hash.
            errores.Add($"No puede superar los {LargoMaximo} caracteres.");
        }

        if (!Minuscula().IsMatch(clave)) errores.Add("Debe incluir una letra minúscula.");
        if (!Mayuscula().IsMatch(clave)) errores.Add("Debe incluir una letra mayúscula.");
        if (!Digito().IsMatch(clave)) errores.Add("Debe incluir un número.");
        if (!Simbolo().IsMatch(clave)) errores.Add("Debe incluir un símbolo.");

        var normalizada = clave.ToLowerInvariant();

        if (Frecuentes.Any(f => normalizada.Contains(f, StringComparison.Ordinal)))
        {
            errores.Add("Contiene una palabra demasiado común. Elegí algo menos previsible.");
        }

        if (!string.IsNullOrWhiteSpace(usuario)
            && normalizada.Contains(usuario.ToLowerInvariant(), StringComparison.Ordinal))
        {
            errores.Add("No puede contener tu nombre de usuario.");
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            var local = email.Split('@')[0].ToLowerInvariant();
            if (local.Length >= 3 && normalizada.Contains(local, StringComparison.Ordinal))
            {
                errores.Add("No puede contener tu correo.");
            }
        }

        if (TieneSecuencia(normalizada))
        {
            errores.Add("Evitá secuencias como \"abcd\" o \"1234\".");
        }

        if (TieneRepeticionLarga(normalizada))
        {
            errores.Add("Evitá repetir el mismo carácter cuatro veces o más.");
        }

        return errores;
    }

    /// <summary>Detecta cuatro caracteres consecutivos en orden ascendente o descendente.</summary>
    private static bool TieneSecuencia(string valor)
    {
        for (var i = 0; i + 3 < valor.Length; i++)
        {
            var d1 = valor[i + 1] - valor[i];
            var d2 = valor[i + 2] - valor[i + 1];
            var d3 = valor[i + 3] - valor[i + 2];

            if (d1 == d2 && d2 == d3 && (d1 == 1 || d1 == -1)) return true;
        }

        return false;
    }

    private static bool TieneRepeticionLarga(string valor)
    {
        for (var i = 0; i + 3 < valor.Length; i++)
        {
            if (valor[i] == valor[i + 1] && valor[i] == valor[i + 2] && valor[i] == valor[i + 3])
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex("[a-zà-ÿ]")] private static partial Regex Minuscula();
    [GeneratedRegex("[A-ZÀ-Ý]")] private static partial Regex Mayuscula();
    [GeneratedRegex("[0-9]")] private static partial Regex Digito();
    [GeneratedRegex(@"[^a-zA-Zà-ÿÀ-Ý0-9]")] private static partial Regex Simbolo();
}
