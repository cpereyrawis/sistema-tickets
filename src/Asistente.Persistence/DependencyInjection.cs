using Asistente.Domain.Services;
using Asistente.Domain.Services.Interfaces;
using Asistente.Persistence.Configuration;
using Asistente.Persistence.Database;
using Asistente.Persistence.Repositories;
using Asistente.Persistence.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Asistente.Persistence;

public static class DependencyInjection
{
    /// <summary>
    /// Registra la persistencia y los servicios de dominio.
    ///
    /// La cadena de conexión se lee de la sección DatabaseSettings y nunca se registra en
    /// logs ni se expone en respuestas (NFR-003, AC-16).
    /// </summary>
    public static IServiceCollection AgregarPersistencia(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));
        services.Configure<RelojSettings>(configuration.GetSection(RelojSettings.SectionName));

        var ajustes = configuration
            .GetSection(DatabaseSettings.SectionName)
            .Get<DatabaseSettings>() ?? new DatabaseSettings();

        services.AddDbContext<AsistenteDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value;

            switch (config.Provider.ToLowerInvariant())
            {
                case "oracle":
                    options.UseOracle(config.ConnectionString, oracle =>
                    {
                        oracle.CommandTimeout(config.CommandTimeoutSeconds);
                        oracle.MigrationsHistoryTable("ASIS_MIGRACIONES", config.Schema);
                    });
                    break;

                // SQLite es el proveedor de DESARROLLO, para poder trabajar cuando la base
                // Oracle no está al alcance. Es relacional, así que respeta transacciones,
                // claves y constraints: a diferencia del proveedor en memoria, no finge
                // comportamientos que la base real no tendría.
                //
                // El destino sigue siendo Oracle. Los índices únicos parciales de
                // db/02-indices-invariantes.sql no aplican acá, así que la defensa de
                // última línea contra dos jornadas abiertas solo existe en Oracle.
                case "sqlite":
                    options.UseSqlite(config.ConnectionString, sqlite =>
                        sqlite.CommandTimeout(config.CommandTimeoutSeconds));
                    break;

                default:
                    throw new NotSupportedException(
                        $"Proveedor de base de datos no soportado: '{config.Provider}'. "
                        + "Valores válidos: Oracle, Sqlite.");
            }
        });

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRelojCorporativo, RelojCorporativo>();
        services.AddScoped<IWorkdayRepository, WorkdayRepository>();
        services.AddScoped<IWorkdayService, WorkdayService>();

        // Fuente de tickets: implementación simulada hasta relevar la vista corporativa.
        services.AddSingleton<ITicketQueryService, TicketQueryServiceSimulado>();

        return services;
    }
}
