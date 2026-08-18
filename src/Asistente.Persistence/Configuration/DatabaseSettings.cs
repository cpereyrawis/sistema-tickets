namespace Asistente.Persistence.Configuration;

/// <summary>
/// Configuración de las DOS bases del sistema.
///
/// Están separadas a propósito y no comparten conexión ni credenciales: la del asistente
/// se escribe, la de tickets solo se lee. Mantenerlas como dos secciones distintas hace
/// difícil confundirlas por accidente y permite darle a cada una su propia cuenta con el
/// mínimo privilegio que necesita (NFR-004, NFR-005).
/// </summary>
public sealed class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";

    /// <summary>Base propia del asistente: jornadas, usuarios, planillas. Lectura y escritura.</summary>
    public AsistenteDbSettings Asistente { get; set; } = new();

    /// <summary>Base corporativa de tickets. Exclusivamente lectura.</summary>
    public TicketsDbSettings Tickets { get; set; } = new();
}

/// <summary>Base del asistente. El destino es SQL Server.</summary>
public sealed class AsistenteDbSettings
{
    /// <summary>"SqlServer" en cualquier entorno real; "Sqlite" solo para desarrollo local.</summary>
    public string Provider { get; set; } = "SqlServer";

    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Esquema donde viven las tablas. En SQL Server el habitual es dbo.</summary>
    public string Schema { get; set; } = "dbo";

    public int CommandTimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Base del sistema de tickets. Oracle, solo lectura.
///
/// El nombre de la vista y de sus columnas son configurables porque el esquema corporativo
/// todavía no se relevó (fase F0-A del plan). Cuando se conozca, apuntar el adaptador es
/// cambiar configuración y no recompilar.
/// </summary>
public sealed class TicketsDbSettings
{
    /// <summary>"Oracle" contra la base real; "Simulado" usa datos en memoria.</summary>
    public string Provider { get; set; } = "Simulado";

    /// <summary>Cadena en formato descriptor TNS, para las consultas con Dapper.</summary>
    public string AdoDataSource { get; set; } = string.Empty;

    public int CommandTimeoutSeconds { get; set; } = 15;

    /// <summary>Tope de filas por consulta, para no castigar la base corporativa (FR-014).</summary>
    public int MaxFilas { get; set; } = 200;

    /// <summary>Vista autorizada de tickets y sus columnas.</summary>
    public MapeoTickets Mapeo { get; set; } = new();
}

/// <summary>
/// Nombres de la vista y las columnas de tickets.
///
/// Se interpolan en el SQL, así que solo pueden contener identificadores válidos: el
/// adaptador los valida antes de usarlos. Los VALORES que filtra el usuario van siempre
/// como parámetros enlazados, nunca concatenados.
/// </summary>
public sealed class MapeoTickets
{
    public string Vista { get; set; } = "VW_ASISTENTE_TICKETS";

    public string ColumnaTicketId { get; set; } = "TICKET_ID";
    public string ColumnaClienteId { get; set; } = "CLIENTE_ID";
    public string ColumnaClienteNombre { get; set; } = "CLIENTE_NOMBRE";
    public string ColumnaTitulo { get; set; } = "TITULO";
    public string ColumnaEstado { get; set; } = "ESTADO";
    public string ColumnaPrioridad { get; set; } = "PRIORIDAD";
    public string ColumnaFechaCreacion { get; set; } = "FECHA_CREACION";

    /// <summary>
    /// Columna con el nombre de usuario asignado. Es la que vincula ambos sistemas: se
    /// filtra por ella con el usuario que inició sesión.
    /// </summary>
    public string ColumnaAsignadoA { get; set; } = "ASIGNADO_A";
}
