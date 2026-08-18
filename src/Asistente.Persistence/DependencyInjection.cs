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
    /// Registra las DOS bases y los servicios de dominio.
    ///
    /// Las cadenas de conexión se leen de configuración y nunca se escriben en logs ni se
    /// exponen en respuestas (NFR-003, AC-16).
    /// </summary>
    public static IServiceCollection AgregarPersistencia(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseSettings>(configuration.GetSection(DatabaseSettings.SectionName));
        services.Configure<RelojSettings>(configuration.GetSection(RelojSettings.SectionName));

        var ajustes = configuration
            .GetSection(DatabaseSettings.SectionName)
            .Get<DatabaseSettings>() ?? new DatabaseSettings();

        RegistrarBaseAsistente(services);
        RegistrarFuenteTickets(services, ajustes.Tickets);

        services.Configure<CorreoSettings>(configuration.GetSection(CorreoSettings.SectionName));

        var auth = configuration.GetSection(AuthSettings.SectionName).Get<AuthSettings>() ?? new AuthSettings();
        services.AddSingleton(auth);

        RegistrarCorreo(services, configuration);

        services.AddSingleton<IHasherClave, HasherClave>();
        services.AddSingleton<IGeneradorTokens, GeneradorTokens>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IRelojCorporativo, RelojCorporativo>();
        services.AddScoped<IWorkdayRepository, WorkdayRepository>();
        services.AddScoped<IWorkdayService, WorkdayService>();

        return services;
    }

    /// <summary>Base propia: SQL Server, con lectura y escritura.</summary>
    private static void RegistrarBaseAsistente(IServiceCollection services)
    {
        services.AddDbContext<AsistenteDbContext>((sp, options) =>
        {
            var config = sp.GetRequiredService<IOptions<DatabaseSettings>>().Value.Asistente;

            switch (config.Provider.ToLowerInvariant())
            {
                case "sqlserver":
                    options.UseSqlServer(config.ConnectionString, sql =>
                    {
                        sql.CommandTimeout(config.CommandTimeoutSeconds);
                        sql.MigrationsHistoryTable("T_MIGRACION", config.Schema);
                        // Reintenta ante fallos transitorios de red o failover, que en una
                        // base remota son esperables y no deberían perder una transición.
                        sql.EnableRetryOnFailure(maxRetryCount: 3, TimeSpan.FromSeconds(5), null);
                    });
                    break;

                // SQLite es el proveedor de DESARROLLO, para trabajar sin instalar nada.
                // Es relacional, así que respeta transacciones, claves y constraints: a
                // diferencia del proveedor en memoria, no finge comportamientos que la
                // base real no tendría. Los índices únicos filtrados sí quedan fuera.
                case "sqlite":
                    options.UseSqlite(config.ConnectionString, sqlite =>
                        sqlite.CommandTimeout(config.CommandTimeoutSeconds));
                    break;

                default:
                    throw new NotSupportedException(
                        $"Proveedor no soportado para la base del asistente: '{config.Provider}'. "
                        + "Valores válidos: SqlServer, Sqlite.");
            }
        });
    }

    /// <summary>
    /// Fuente de tickets: Oracle, exclusivamente lectura.
    ///
    /// No se registra un DbContext: el adaptador abre su propia conexión con Dapper por
    /// consulta. Así queda claro que este origen no participa de las transacciones del
    /// asistente y que nada de lo que pase acá puede escribir en la base corporativa.
    /// </summary>
    private static void RegistrarFuenteTickets(IServiceCollection services, TicketsDbSettings config)
    {
        switch (config.Provider.ToLowerInvariant())
        {
            case "oracle":
                services.AddSingleton<ITicketQueryService, TicketQueryServiceOracle>();
                break;

            case "simulado":
                services.AddSingleton<ITicketQueryService, TicketQueryServiceSimulado>();
                break;

            default:
                throw new NotSupportedException(
                    $"Proveedor no soportado para la fuente de tickets: '{config.Provider}'. "
                    + "Valores válidos: Oracle, Simulado.");
        }
    }

    /// <summary>
    /// Proveedor de correo. El de archivo permite probar activación y restablecimiento
    /// sin servidor SMTP: escribe cada mensaje en disco para abrirlo y hacer clic.
    /// </summary>
    private static void RegistrarCorreo(IServiceCollection services, IConfiguration configuration)
    {
        var config = configuration.GetSection(CorreoSettings.SectionName).Get<CorreoSettings>()
                     ?? new CorreoSettings();

        switch (config.Proveedor.ToLowerInvariant())
        {
            case "smtp":
                services.AddScoped<IServicioCorreo, ServicioCorreoSmtp>();
                break;

            case "archivo":
                services.AddScoped<IServicioCorreo, ServicioCorreoArchivo>();
                break;

            default:
                throw new NotSupportedException(
                    $"Proveedor de correo no soportado: '{config.Proveedor}'. Valores válidos: Smtp, Archivo.");
        }
    }
}
