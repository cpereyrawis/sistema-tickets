using Asistente.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Asistente.Persistence.Database;

/// <summary>
/// Fábrica que usan las herramientas de EF Core en tiempo de diseño (dotnet ef).
///
/// Lee la misma sección DatabaseSettings que la aplicación, de modo que las migraciones y
/// los scripts se generan contra la configuración real y no contra una cadena escrita a
/// mano que puede quedar desfasada.
/// </summary>
public sealed class AsistenteDbContextFactory : IDesignTimeDbContextFactory<AsistenteDbContext>
{
    public AsistenteDbContext CreateDbContext(string[] args)
    {
        var raiz = Directory.GetCurrentDirectory();
        var apiPath = Path.GetFullPath(Path.Combine(raiz, "..", "Asistente.Api"));
        var basePath = Directory.Exists(apiPath) ? apiPath : raiz;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var ajustes = configuration
            .GetSection(DatabaseSettings.SectionName)
            .Get<DatabaseSettings>() ?? new DatabaseSettings();

        var asistente = ajustes.Asistente;

        var builder = new DbContextOptionsBuilder<AsistenteDbContext>();

        // Para generar un script no hace falta una conexión viva, pero el proveedor exige
        // una cadena con forma válida. Se genera SIEMPRE contra SQL Server: el script que
        // se ejecuta en la base real es ese, aunque en desarrollo se trabaje con SQLite.
        var cadena = string.IsNullOrWhiteSpace(asistente.ConnectionString)
            || !asistente.Provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
                ? "Server=localhost;Database=Asistente;Trusted_Connection=True;TrustServerCertificate=True"
                : asistente.ConnectionString;

        // El esquema se normaliza antes de construir el contexto para que el modelo y el
        // historial de migraciones coincidan; si discreparan, EF crearía la tabla de
        // historial en un esquema y las tablas en otro.
        var esquema = EsquemaEfectivo(asistente);
        asistente.Schema = esquema;

        builder.UseSqlServer(cadena, sql => sql.MigrationsHistoryTable("T_MIGRACION", esquema));

        return new AsistenteDbContext(
            builder.Options, Microsoft.Extensions.Options.Options.Create(ajustes));
    }

    /// <summary>El esquema puede venir vacío para SQLite; en SQL Server el habitual es dbo.</summary>
    private static string EsquemaEfectivo(AsistenteDbSettings asistente) =>
        string.IsNullOrWhiteSpace(asistente.Schema) ? "dbo" : asistente.Schema;
}
