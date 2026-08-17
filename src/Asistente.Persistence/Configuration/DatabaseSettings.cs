namespace Asistente.Persistence.Configuration;

/// <summary>
/// Configuración de acceso a datos. Mantiene la forma de la sección DatabaseSettings de
/// WISBase40 para que un desarrollador que viene de ese proyecto la reconozca.
/// </summary>
public sealed class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";

    /// <summary>Motor de base de datos. Hoy solo se soporta "Oracle".</summary>
    public string Provider { get; set; } = "Oracle";

    /// <summary>Cadena que usa EF Core para la base del asistente.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Cadena de Oracle guardada aparte mientras se trabaja con el proveedor de
    /// desarrollo, para no perderla ni tener que reescribirla al volver a Oracle.
    /// </summary>
    public string OracleConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Cadena en formato descriptor TNS, para las consultas con Dapper/ADO que no pasan
    /// por EF Core.
    /// </summary>
    public string AdoDataSource { get; set; } = string.Empty;

    /// <summary>
    /// Tope de parámetros por consulta. El límite real de Oracle es mayor, pero conviene
    /// segmentar en ráfagas antes que enviar una única sentencia enorme.
    /// </summary>
    public int MaxParameterCountPerQuery { get; set; } = 2099;

    /// <summary>Esquema donde viven las tablas del asistente.</summary>
    public string Schema { get; set; } = "MAOSOL";

    /// <summary>Timeout de comando en segundos, para no castigar la base ante una consulta pesada.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;
}
