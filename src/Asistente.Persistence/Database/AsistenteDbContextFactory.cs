using Asistente.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Asistente.Persistence.Database;

/// <summary>
/// Fábrica que usan las herramientas de EF Core en tiempo de diseño (dotnet ef).
///
/// Lee la misma sección DatabaseSettings que la aplicación, de modo que las migraciones
/// se generan contra la configuración real y no contra una cadena escrita a mano.
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

        // Para generar el script no hace falta una conexión viva, pero el proveedor exige
        // una cadena con forma válida.
        var cadena = string.IsNullOrWhiteSpace(ajustes.ConnectionString)
            ? "DATA SOURCE=localhost:1521/ORCLCDB;USER ID=MAOSOL;PASSWORD=MAOSOL"
            : ajustes.ConnectionString;

        var options = new DbContextOptionsBuilder<AsistenteDbContext>()
            .UseOracle(cadena, o => o.MigrationsHistoryTable("ASIS_MIGRACIONES", ajustes.Schema))
            .Options;

        return new AsistenteDbContext(options, Microsoft.Extensions.Options.Options.Create(ajustes));
    }
}
