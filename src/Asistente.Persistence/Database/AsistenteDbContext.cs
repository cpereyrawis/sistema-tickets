using Asistente.Domain.Entities;
using Asistente.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asistente.Persistence.Database;

/// <summary>
/// Base propia del asistente. Es intencionalmente independiente del sistema de tickets:
/// acá se escribe, allá solo se lee (§11.1, decisión clave de la especificación).
///
/// Nomenclatura Oracle: tablas y columnas en MAYÚSCULAS con prefijo ASIS_, para que
/// convivan sin ambigüedad con lo que ya exista en el esquema.
/// </summary>
public sealed class AsistenteDbContext : DbContext
{
    private readonly string _schema;

    public AsistenteDbContext(
        DbContextOptions<AsistenteDbContext> options,
        IOptions<DatabaseSettings> settings)
        : base(options)
    {
        _schema = settings.Value.Schema;
    }

    public DbSet<AppUser> Usuarios => Set<AppUser>();
    public DbSet<Workday> Jornadas => Set<Workday>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Aplica a todas las fechas del modelo, para no depender de recordar el converter
        // en cada propiedad nueva. FechaLocal queda fuera: es DateOnly, no DateTime.
        builder.Properties<DateTime>().HaveConversion<ConversorUtc>();
        builder.Properties<DateTime?>().HaveConversion<ConversorUtcNullable>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // SQLite no tiene esquemas: cuando se configura vacío, las tablas quedan sueltas.
        if (!string.IsNullOrWhiteSpace(_schema))
        {
            modelBuilder.HasDefaultSchema(_schema);
        }

        ConfigurarUsuario(modelBuilder);
        ConfigurarJornada(modelBuilder);
        ConfigurarSesion(modelBuilder);
        ConfigurarEvento(modelBuilder);
        ConfigurarAuditoria(modelBuilder);
    }

    private static void ConfigurarUsuario(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.ToTable("ASIS_USUARIO");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.ExternalUserId).HasColumnName("EXTERNAL_USER_ID").HasMaxLength(64).IsRequired();
            e.Property(x => x.Usuario).HasColumnName("USUARIO").HasMaxLength(64).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("DISPLAY_NAME").HasMaxLength(120).IsRequired();
            e.Property(x => x.IsActive).HasColumnName("ACTIVO").IsRequired();
            e.Property(x => x.LastLoginAtUtc).HasColumnName("ULTIMO_INGRESO_UTC");

            e.HasIndex(x => x.ExternalUserId).IsUnique().HasDatabaseName("UX_ASIS_USUARIO_EXT");
            e.HasIndex(x => x.Usuario).IsUnique().HasDatabaseName("UX_ASIS_USUARIO_LOGIN");
        });
    }

    private static void ConfigurarJornada(ModelBuilder b)
    {
        b.Entity<Workday>(e =>
        {
            e.ToTable("ASIS_JORNADA");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("USUARIO_ID").IsRequired();

            // Oracle no tiene un tipo DATE sin hora; se persiste como DATE y se convierte.
            e.Property(x => x.FechaLocal)
                .HasColumnName("FECHA_LOCAL")
                .HasConversion(
                    v => v.ToDateTime(TimeOnly.MinValue),
                    v => DateOnly.FromDateTime(v))
                .IsRequired();

            e.Property(x => x.InicioUtc).HasColumnName("INICIO_UTC").IsRequired();
            e.Property(x => x.FinUtc).HasColumnName("FIN_UTC");
            e.Property(x => x.Estado).HasColumnName("ESTADO").HasConversion<int>().IsRequired();

            // Concurrencia optimista: Oracle no tiene rowversion, así que se usa un
            // contador que EF compara e incrementa en cada UPDATE (§6.2 del plan).
            e.Property(x => x.Version).HasColumnName("VERSION").IsConcurrencyToken().IsRequired();

            e.OwnsOne(x => x.TicketPrincipal, t =>
            {
                t.Property(p => p.TicketId).HasColumnName("TICKET_PRINCIPAL_ID").HasMaxLength(24);
                t.Property(p => p.ClienteId).HasColumnName("TICKET_PRINCIPAL_CLIENTE_ID").HasMaxLength(16);
                t.Property(p => p.ClienteNombre).HasColumnName("TICKET_PRINCIPAL_CLIENTE").HasMaxLength(120);
                t.Property(p => p.Titulo).HasColumnName("TICKET_PRINCIPAL_TITULO").HasMaxLength(200);
            });

            e.HasIndex(x => new { x.UserId, x.FechaLocal }).HasDatabaseName("IX_ASIS_JORNADA_USR_FECHA");

            e.Navigation(x => x.Sesiones).HasField("_sesiones").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.Eventos).HasField("_eventos").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.Auditoria).HasField("_auditoria").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private static void ConfigurarSesion(ModelBuilder b)
    {
        b.Entity<WorkSession>(e =>
        {
            e.ToTable("ASIS_SESION", t => t.HasCheckConstraint(
                "CK_ASIS_SESION_FIN",
                // Invariante §6.1 aplicado en la base, no solo en el código.
                "FIN_UTC IS NULL OR FIN_UTC >= INICIO_UTC"));

            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.WorkdayId).HasColumnName("JORNADA_ID").IsRequired();
            e.Property(x => x.Tipo).HasColumnName("TIPO").HasConversion<int>().IsRequired();
            e.Property(x => x.InicioUtc).HasColumnName("INICIO_UTC").IsRequired();
            e.Property(x => x.FinUtc).HasColumnName("FIN_UTC");
            e.Property(x => x.AccionOrigen).HasColumnName("ACCION_ORIGEN").HasConversion<int>().IsRequired();
            e.Property(x => x.Editada).HasColumnName("EDITADA").IsRequired();

            e.OwnsOne(x => x.Ticket, t =>
            {
                t.Property(p => p.TicketId).HasColumnName("TICKET_ID").HasMaxLength(24).IsRequired();
                t.Property(p => p.ClienteId).HasColumnName("CLIENTE_ID").HasMaxLength(16).IsRequired();
                t.Property(p => p.ClienteNombre).HasColumnName("CLIENTE_NOMBRE").HasMaxLength(120).IsRequired();
                t.Property(p => p.Titulo).HasColumnName("TITULO").HasMaxLength(200).IsRequired();
            });

            e.HasOne<Workday>()
                .WithMany(x => x.Sesiones)
                .HasForeignKey(x => x.WorkdayId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.WorkdayId, x.InicioUtc }).HasDatabaseName("IX_ASIS_SESION_JOR_INI");
        });
    }

    private static void ConfigurarEvento(ModelBuilder b)
    {
        b.Entity<TimeEvent>(e =>
        {
            e.ToTable("ASIS_EVENTO");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.WorkdayId).HasColumnName("JORNADA_ID").IsRequired();
            e.Property(x => x.Tipo).HasColumnName("TIPO").HasConversion<int>().IsRequired();
            e.Property(x => x.TicketId).HasColumnName("TICKET_ID").HasMaxLength(24).IsRequired();
            e.Property(x => x.OcurridoEnUtc).HasColumnName("OCURRIDO_EN_UTC").IsRequired();
            e.Property(x => x.CorrelationId).HasColumnName("CORRELATION_ID").IsRequired();
            e.Property(x => x.CreadoEnUtc).HasColumnName("CREADO_EN_UTC").IsRequired();

            e.HasOne<Workday>()
                .WithMany(x => x.Eventos)
                .HasForeignKey(x => x.WorkdayId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.CorrelationId).HasDatabaseName("IX_ASIS_EVENTO_CORR");
            e.HasIndex(x => new { x.WorkdayId, x.OcurridoEnUtc }).HasDatabaseName("IX_ASIS_EVENTO_JOR_OCU");
        });
    }

    private static void ConfigurarAuditoria(ModelBuilder b)
    {
        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("ASIS_AUDITORIA");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.WorkdayId).HasColumnName("JORNADA_ID").IsRequired();
            e.Property(x => x.Accion).HasColumnName("ACCION").HasMaxLength(60).IsRequired();
            e.Property(x => x.OcurridoEnUtc).HasColumnName("OCURRIDO_EN_UTC").IsRequired();
            e.Property(x => x.UserId).HasColumnName("USUARIO_ID").IsRequired();
            e.Property(x => x.Detalle).HasColumnName("DETALLE").HasMaxLength(500).IsRequired();

            e.HasOne<Workday>()
                .WithMany(x => x.Auditoria)
                .HasForeignKey(x => x.WorkdayId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
