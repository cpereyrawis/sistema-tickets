namespace Asistente.Common;

/// <summary>
/// Resultado de una operación de dominio. Evita usar excepciones para el flujo de
/// negocio esperable: un rechazo por regla no es un error del programa.
/// </summary>
public readonly struct Resultado
{
    private Resultado(bool ok, string? codigo, string? mensaje)
    {
        Ok = ok;
        Codigo = codigo;
        Mensaje = mensaje;
    }

    public bool Ok { get; }

    /// <summary>Código estable para que la API lo mapee a un status HTTP.</summary>
    public string? Codigo { get; }

    /// <summary>Mensaje para el usuario final, ya en su idioma.</summary>
    public string? Mensaje { get; }

    public static Resultado Exito() => new(true, null, null);

    public static Resultado Fallo(string codigo, string mensaje) => new(false, codigo, mensaje);
}

/// <summary>Resultado que además transporta un valor cuando la operación tuvo éxito.</summary>
public readonly struct Resultado<T>
{
    private Resultado(bool ok, T? valor, string? codigo, string? mensaje)
    {
        Ok = ok;
        Valor = valor;
        Codigo = codigo;
        Mensaje = mensaje;
    }

    public bool Ok { get; }
    public T? Valor { get; }
    public string? Codigo { get; }
    public string? Mensaje { get; }

    public static Resultado<T> Exito(T valor) => new(true, valor, null, null);

    public static Resultado<T> Fallo(string codigo, string mensaje) =>
        new(false, default, codigo, mensaje);

    public static Resultado<T> Fallo(Resultado otro) =>
        new(false, default, otro.Codigo, otro.Mensaje);

    /// <summary>Propaga el fallo de un resultado que transportaba otro tipo de valor.</summary>
    public static Resultado<T> Fallo<TOtro>(Resultado<TOtro> otro) =>
        new(false, default, otro.Codigo, otro.Mensaje);
}
