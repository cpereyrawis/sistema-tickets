using System.Text.Json.Serialization;
using Asistente.Api.Security;
using Asistente.Persistence;
using Asistente.Persistence.Configuration;
using Asistente.Persistence.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(o =>
    {
        // Los enums viajan como texto ("Activa", "FinTarea") y no como números: el
        // contrato con el frontend queda legible y no se rompe si mañana se inserta un
        // valor en el medio del enum.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Asistente de Registro de Tareas",
        Version = "v1",
        Description =
            "API interna del asistente. La jornada, sus sesiones y su auditoría viven en la "
            + "base propia; los tickets se consultan de solo lectura.",
    });

    var xml = Path.Combine(AppContext.BaseDirectory, "Asistente.Api.xml");
    if (File.Exists(xml)) options.IncludeXmlComments(xml);
});

builder.Services.AgregarPersistencia(builder.Configuration);

// Identidad: hoy resuelve por cabecera en desarrollo. Cuando exista el mecanismo
// corporativo se registra otra implementación de IUsuarioActual y nada más cambia.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddScoped<IUsuarioActual, UsuarioActualDesarrollo>();
}
else
{
    // Arrancar en producción con la identidad de desarrollo dejaría la API abierta:
    // preferimos que la aplicación no levante a que levante insegura.
    throw new InvalidOperationException(
        "No hay un mecanismo de identidad configurado para este entorno. "
        + "Registrá una implementación de IUsuarioActual antes de desplegar fuera de desarrollo.");
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Asistente v1"));

    // La base de desarrollo SQLite se crea sola, porque es descartable y vive en un
    // archivo local. Contra Oracle NUNCA se toca el esquema desde la aplicación: ahí los
    // objetos se crean ejecutando los scripts de db/ a mano.
    using var scope = app.Services.CreateScope();
    var ajustes = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

    if (string.Equals(ajustes.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var db = scope.ServiceProvider.GetRequiredService<AsistenteDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}

app.UseHttpsRedirection();
app.MapControllers();

// Diagnóstico de conectividad. No expone la cadena de conexión ni credenciales (AC-16):
// solo dice si la base responde.
app.MapGet("/api/salud/base", async (AsistenteDbContext db, CancellationToken ct) =>
{
    try
    {
        var puede = await db.Database.CanConnectAsync(ct);
        return puede
            ? Results.Ok(new { estado = "conectado" })
            : Results.Problem("La base no respondió.", statusCode: 503);
    }
    catch (Exception ex)
    {
        // Se informa el tipo de fallo, nunca el mensaje crudo del driver, que suele
        // incluir host y usuario.
        return Results.Problem(
            $"No se pudo conectar a la base ({ex.GetType().Name}).", statusCode: 503);
    }
});

app.Run();
