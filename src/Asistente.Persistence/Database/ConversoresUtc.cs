using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Asistente.Persistence.Database;

/// <summary>
/// Fuerza que todo <see cref="DateTime"/> vuelva de la base marcado como UTC.
///
/// Ni Oracle ni SQLite guardan la zona junto al instante, así que al releer devuelven
/// <see cref="DateTimeKind.Unspecified"/>. Sin esto, el valor se serializa sin la "Z" y
/// el navegador lo interpreta como hora local: todos los tramos quedarían corridos el
/// equivalente al huso horario, en silencio.
/// </summary>
public sealed class ConversorUtc : ValueConverter<DateTime, DateTime>
{
    public ConversorUtc()
        : base(
            v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

/// <inheritdoc cref="ConversorUtc"/>
public sealed class ConversorUtcNullable : ValueConverter<DateTime?, DateTime?>
{
    public ConversorUtcNullable()
        : base(
            v => v.HasValue
                ? (v.Value.Kind == DateTimeKind.Utc ? v.Value : v.Value.ToUniversalTime())
                : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}
