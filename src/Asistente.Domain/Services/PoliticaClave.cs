namespace Asistente.Domain.Services;

/// <summary>
/// Reglas de fortaleza de contraseña.
///
/// DELIBERADAMENTE MÍNIMAS. La versión anterior exigía doce caracteres con mayúscula,
/// minúscula, número y símbolo, y rechazaba palabras frecuentes y secuencias. Se relajó
/// al adoptar una contraseña por defecto numérica y corta para toda la nómina: una
/// política que la propia semilla incumple no es una política, es una excepción
/// permanente esperando a que alguien la descubra.
///
/// Queda solo el largo. El tope máximo no es una regla de fortaleza sino una defensa:
/// evita que una entrada enorme dispare un cómputo de hash costoso.
/// </summary>
public static class PoliticaClave
{
    public const int LargoMinimo = 4;
    public const int LargoMaximo = 128;

    public static IReadOnlyList<string> Validar(string? clave)
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
            errores.Add($"No puede superar los {LargoMaximo} caracteres.");
        }

        return errores;
    }
}
