namespace Asistente.Domain.Services.Interfaces;

/// <summary>
/// Reloj del sistema. Se abstrae para poder congelarlo en pruebas y para concentrar en
/// un solo lugar la conversión a la zona corporativa (NFR-012): todo se persiste en UTC
/// y solo se convierte al mostrar o exportar.
/// </summary>
public interface IRelojCorporativo
{
    DateTime AhoraUtc { get; }

    /// <summary>Fecha operativa local que corresponde a un instante UTC.</summary>
    DateOnly FechaLocal(DateTime instanteUtc);

    /// <summary>Convierte un instante UTC a la hora de la zona corporativa.</summary>
    DateTime AHoraLocal(DateTime instanteUtc);
}
