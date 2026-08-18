namespace Asistente.Domain.Entities;

/// <summary>
/// Planilla Excel generada a partir de una jornada (FR-044, FR-045).
///
/// Se conserva el archivo además de sus metadatos porque el importador corporativo recibe
/// ese archivo exacto: si más adelante hay una discrepancia, poder recuperar el que
/// realmente se entregó es la única forma de dirimirla. El hash permite verificar que lo
/// almacenado es lo que se descargó.
/// </summary>
public sealed class Planilla
{
    private Planilla() { }

    public Planilla(
        long workdayId,
        long userId,
        DateTime generadaEnUtc,
        string nombreArchivo,
        string hashSha256,
        int cantidadFilas,
        int numeroGeneracion,
        byte[]? contenido)
    {
        WorkdayId = workdayId;
        UserId = userId;
        GeneradaEnUtc = generadaEnUtc;
        NombreArchivo = nombreArchivo;
        HashSha256 = hashSha256;
        CantidadFilas = cantidadFilas;
        NumeroGeneracion = numeroGeneracion;
        Contenido = contenido;
    }

    public long Id { get; private set; }
    public long WorkdayId { get; private set; }
    public long UserId { get; private set; }

    public DateTime GeneradaEnUtc { get; private set; }
    public string NombreArchivo { get; private set; } = string.Empty;
    public string HashSha256 { get; private set; } = string.Empty;
    public int CantidadFilas { get; private set; }

    /// <summary>
    /// 1 para la primera generación, 2 en adelante para las regeneraciones. Permite
    /// identificarlas sin borrar la anterior, que es lo que pide FR-045.
    /// </summary>
    public int NumeroGeneracion { get; private set; }

    public bool EsRegeneracion => NumeroGeneracion > 1;

    /// <summary>Bytes del .xlsx. Nulo si se decide no conservar el archivo.</summary>
    public byte[]? Contenido { get; private set; }
}
