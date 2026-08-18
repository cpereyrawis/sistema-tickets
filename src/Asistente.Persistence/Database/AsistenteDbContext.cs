using Asistente.Domain.Entities;
using Asistente.Persistence.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Asistente.Persistence.Database;

/// <summary>
/// Base propia del asistente, sobre SQL Server.
///
/// Es independiente del sistema de tickets: acá se escribe, allá solo se lee. El único
/// vínculo entre ambas es el nombre de usuario (<see cref="AppUser.Usuario"/>); no hay
/// claves foráneas cruzadas ni consultas que mezclen ambos orígenes, de modo que una
/// caída de la base corporativa no compromete lo ya registrado (NFR-014).
///
/// CONVENCIÓN DE NOMBRES en la base, distinta de la del código C#:
///   · Tablas con prefijo T_ y vistas con V_, en MAYÚSCULA y en singular: T_USUARIO.
///   · Columnas en MAYÚSCULA y en singular, separadas con guion bajo: NOMBRE_COMPLETO.
///
/// Por eso cada propiedad lleva su <c>HasColumnName</c> explícito: sin él, EF Core usaría
/// el nombre de la propiedad C# y el esquema terminaría mezclando dos estilos.
/// </summary>
public sealed class AsistenteDbContext : DbContext
{
    private readonly string _schema;

    public AsistenteDbContext(
        DbContextOptions<AsistenteDbContext> options,
        IOptions<DatabaseSettings> settings)
        : base(options)
    {
        _schema = settings.Value.Asistente.Schema;
    }

    public DbSet<AppUser> Usuarios => Set<AppUser>();
    public DbSet<SesionUsuario> SesionesUsuario => Set<SesionUsuario>();
    public DbSet<Workday> Jornadas => Set<Workday>();
    public DbSet<Planilla> Planillas => Set<Planilla>();
    public DbSet<TokenUsuario> Tokens => Set<TokenUsuario>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Aplica a todas las fechas del modelo, para no depender de recordar el converter
        // en cada propiedad nueva. FECHA_LOCAL queda fuera: es DateOnly, no DateTime.
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
        ConfigurarSesionUsuario(modelBuilder);
        ConfigurarToken(modelBuilder);
        ConfigurarJornada(modelBuilder);
        ConfigurarSesion(modelBuilder);
        ConfigurarEvento(modelBuilder);
        ConfigurarAuditoria(modelBuilder);
        ConfigurarPlanilla(modelBuilder);
    }

    private static void ConfigurarUsuario(ModelBuilder b)
    {
        b.Entity<AppUser>(e =>
        {
            e.ToTable("T_USUARIO");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.Usuario).HasColumnName("USUARIO").HasMaxLength(64).IsRequired();
            e.Property(x => x.Email).HasColumnName("EMAIL").HasMaxLength(160).IsRequired();
            e.Property(x => x.NombreCompleto).HasColumnName("NOMBRE_COMPLETO").HasMaxLength(120).IsRequired();
            e.Property(x => x.ClaveHash).HasColumnName("CLAVE_HASH").HasMaxLength(256).IsRequired();
            e.Property(x => x.Activo).HasColumnName("ACTIVO").IsRequired();
            e.Property(x => x.EmailVerificado).HasColumnName("EMAIL_VERIFICADO").IsRequired();
            e.Property(x => x.FechaAltaUtc).HasColumnName("FECHA_ALTA_UTC").IsRequired();
            e.Property(x => x.EmailVerificadoEnUtc).HasColumnName("EMAIL_VERIFICADO_EN_UTC");
            e.Property(x => x.UltimoIngresoUtc).HasColumnName("ULTIMO_INGRESO_UTC");
            e.Property(x => x.UltimoCambioClaveUtc).HasColumnName("ULTIMO_CAMBIO_CLAVE_UTC");

            // Es un contador, no una entidad. Se nombra como cantidad para poder mantener
            // el sustantivo en singular sin que el nombre mienta sobre lo que guarda.
            e.Property(x => x.IntentosFallidos).HasColumnName("CANTIDAD_INTENTO_FALLIDO").IsRequired();

            e.Property(x => x.BloqueadoHastaUtc).HasColumnName("BLOQUEADO_HASTA_UTC");

            // Único e insensible a mayúsculas: el nombre viaja al sistema de tickets, y
            // permitir "cpereyra" y "CPereyra" como cuentas distintas rompería el vínculo.
            e.HasIndex(x => x.Usuario).IsUnique().HasDatabaseName("UX_T_USUARIO_USUARIO");
            e.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_T_USUARIO_EMAIL");
        });
    }

    private static void ConfigurarSesionUsuario(ModelBuilder b)
    {
        b.Entity<SesionUsuario>(e =>
        {
            e.ToTable("T_SESION_USUARIO");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("USUARIO_ID").IsRequired();
            e.Property(x => x.InicioUtc).HasColumnName("INICIO_UTC").IsRequired();
            e.Property(x => x.FinUtc).HasColumnName("FIN_UTC");
            e.Property(x => x.DireccionIp).HasColumnName("DIRECCION_IP").HasMaxLength(45);
            e.Property(x => x.Agente).HasColumnName("AGENTE").HasMaxLength(200);
            e.Property(x => x.MotivoCierre).HasColumnName("MOTIVO_CIERRE").HasMaxLength(40);

            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.UserId, x.InicioUtc })
                .HasDatabaseName("IX_T_SESION_USUARIO_USUARIO_INICIO");
        });
    }

    private static void ConfigurarToken(ModelBuilder b)
    {
        b.Entity<TokenUsuario>(e =>
        {
            e.ToTable("T_TOKEN_USUARIO");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("USUARIO_ID").IsRequired();
            e.Property(x => x.Tipo).HasColumnName("TIPO").HasConversion<int>().IsRequired();

            // Longitud fija: es un SHA-256 en hexadecimal.
            e.Property(x => x.TokenHash).HasColumnName("TOKEN_HASH").HasMaxLength(64).IsRequired();

            e.Property(x => x.CreadoEnUtc).HasColumnName("CREADO_EN_UTC").IsRequired();
            e.Property(x => x.ExpiraEnUtc).HasColumnName("EXPIRA_EN_UTC").IsRequired();
            e.Property(x => x.UsadoEnUtc).HasColumnName("USADO_EN_UTC");
            e.Property(x => x.AnuladoEnUtc).HasColumnName("ANULADO_EN_UTC");

            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // La búsqueda siempre es por hash y tipo: sin este índice cada validación de
            // enlace recorrería la tabla entera.
            e.HasIndex(x => new { x.TokenHash, x.Tipo })
                .IsUnique()
                .HasDatabaseName("UX_T_TOKEN_USUARIO_HASH_TIPO");

            e.HasIndex(x => new { x.UserId, x.Tipo })
                .HasDatabaseName("IX_T_TOKEN_USUARIO_USUARIO_TIPO");
        });
    }

    private static void ConfigurarJornada(ModelBuilder b)
    {
        b.Entity<Workday>(e =>
        {
            e.ToTable("T_JORNADA");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.UserId).HasColumnName("USUARIO_ID").IsRequired();

            e.Property(x => x.FechaLocal)
                .HasColumnName("FECHA_LOCAL")
                .HasConversion(
                    v => v.ToDateTime(TimeOnly.MinValue),
                    v => DateOnly.FromDateTime(v))
                .IsRequired();

            e.Property(x => x.InicioUtc).HasColumnName("INICIO_UTC").IsRequired();
            e.Property(x => x.FinUtc).HasColumnName("FIN_UTC");
            e.Property(x => x.Estado).HasColumnName("ESTADO").HasConversion<int>().IsRequired();

            // Concurrencia optimista con un contador propio en vez del rowversion nativo
            // de SQL Server: así el mismo modelo sirve para el proveedor SQLite de
            // desarrollo, que no lo soporta.
            e.Property(x => x.Version).HasColumnName("VERSION").IsConcurrencyToken().IsRequired();

            e.OwnsOne(x => x.TicketPrincipal, t =>
            {
                t.Property(p => p.TicketId).HasColumnName("TICKET_PRINCIPAL_ID").HasMaxLength(24);
                t.Property(p => p.ClienteId).HasColumnName("TICKET_PRINCIPAL_CLIENTE_ID").HasMaxLength(16);
                t.Property(p => p.ClienteNombre).HasColumnName("TICKET_PRINCIPAL_CLIENTE_NOMBRE").HasMaxLength(120);
                t.Property(p => p.Titulo).HasColumnName("TICKET_PRINCIPAL_TITULO").HasMaxLength(200);
            });

            e.HasOne<AppUser>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.UserId, x.FechaLocal })
                .HasDatabaseName("IX_T_JORNADA_USUARIO_FECHA");

            e.Navigation(x => x.Sesiones).HasField("_sesiones").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.Eventos).HasField("_eventos").UsePropertyAccessMode(PropertyAccessMode.Field);
            e.Navigation(x => x.Auditoria).HasField("_auditoria").UsePropertyAccessMode(PropertyAccessMode.Field);
        });
    }

    private static void ConfigurarSesion(ModelBuilder b)
    {
        b.Entity<WorkSession>(e =>
        {
            e.ToTable("T_SESION", t => t.HasCheckConstraint(
                "CK_T_SESION_FIN",
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

            e.HasIndex(x => new { x.WorkdayId, x.InicioUtc })
                .HasDatabaseName("IX_T_SESION_JORNADA_INICIO");
        });
    }

    private static void ConfigurarEvento(ModelBuilder b)
    {
        b.Entity<TimeEvent>(e =>
        {
            e.ToTable("T_EVENTO");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.WorkdayId).HasColumnName("JORNADA_ID").IsRequired();
            e.Property(x => x.Tipo).HasColumnName("TIPO").HasConversion<int>().IsRequired();
            e.Property(x => x.TicketId).HasColumnName("TICKET_ID").HasMaxLength(24).IsRequired();
            e.Property(x => x.OcurridoEnUtc).HasColumnName("OCURRIDO_EN_UTC").IsRequired();
            e.Property(x => x.CorrelationId).HasColumnName("CORRELACION_ID").IsRequired();
            e.Property(x => x.CreadoEnUtc).HasColumnName("CREADO_EN_UTC").IsRequired();

            e.HasOne<Workday>()
                .WithMany(x => x.Eventos)
                .HasForeignKey(x => x.WorkdayId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => x.CorrelationId).HasDatabaseName("IX_T_EVENTO_CORRELACION");
            e.HasIndex(x => new { x.WorkdayId, x.OcurridoEnUtc })
                .HasDatabaseName("IX_T_EVENTO_JORNADA_OCURRIDO");
        });
    }

    private static void ConfigurarAuditoria(ModelBuilder b)
    {
        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("T_AUDITORIA");
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

            e.HasIndex(x => x.WorkdayId).HasDatabaseName("IX_T_AUDITORIA_JORNADA");
        });
    }

    private static void ConfigurarPlanilla(ModelBuilder b)
    {
        b.Entity<Planilla>(e =>
        {
            e.ToTable("T_PLANILLA");
            e.HasKey(x => x.Id);

            e.Property(x => x.Id).HasColumnName("ID").ValueGeneratedOnAdd();
            e.Property(x => x.WorkdayId).HasColumnName("JORNADA_ID").IsRequired();
            e.Property(x => x.UserId).HasColumnName("USUARIO_ID").IsRequired();
            e.Property(x => x.GeneradaEnUtc).HasColumnName("GENERADA_EN_UTC").IsRequired();
            e.Property(x => x.NombreArchivo).HasColumnName("NOMBRE_ARCHIVO").HasMaxLength(120).IsRequired();
            e.Property(x => x.HashSha256).HasColumnName("HASH_SHA256").HasMaxLength(64).IsRequired();
            e.Property(x => x.CantidadFilas).HasColumnName("CANTIDAD_FILA").IsRequired();
            e.Property(x => x.NumeroGeneracion).HasColumnName("NUMERO_GENERACION").IsRequired();

            // El .xlsx puede pesar. EF lo carga junto con la fila, así que las consultas
            // que solo listan metadatos deben proyectar con Select y no traer la entidad
            // completa, o arrastrarían todos los archivos a memoria.
            e.Property(x => x.Contenido).HasColumnName("CONTENIDO");

            e.HasOne<Workday>()
                .WithMany()
                .HasForeignKey(x => x.WorkdayId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.WorkdayId, x.NumeroGeneracion })
                .IsUnique()
                .HasDatabaseName("UX_T_PLANILLA_JORNADA_GENERACION");
        });
    }
}
