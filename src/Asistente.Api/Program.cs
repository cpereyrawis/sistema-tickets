using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Asistente.Api.Security;
using Asistente.Domain.Services;
using Asistente.Persistence;
using Asistente.Persistence.Configuration;
using Asistente.Persistence.Database;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
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
    });

    var xml = Path.Combine(AppContext.BaseDirectory, "Asistente.Api.xml");
    if (File.Exists(xml)) options.IncludeXmlComments(xml);
});

builder.Services.AgregarPersistencia(builder.Configuration);

// ---------- Autenticación por cookie ----------
//
// La cookie la cifra y firma el servidor con la protección de datos de ASP.NET Core: el
// navegador solo guarda un blob opaco, nunca el id ni el nombre del usuario en claro.
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "asistente.sesion";

        // HttpOnly: JavaScript no puede leerla, así que un XSS no puede robar la sesión.
        options.Cookie.HttpOnly = true;

        // Solo por HTTPS. En desarrollo se permite HTTP para no exigir certificado.
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;

        // Strict: el navegador no la envía en peticiones originadas por otro sitio, que es
        // la defensa principal contra CSRF. Se puede porque el frontend y la API comparten
        // origen y no hay redirección desde ningún proveedor externo.
        options.Cookie.SameSite = SameSiteMode.Strict;

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // Es una API: sin esto, una petición no autenticada respondería 302 al login y el
        // frontend recibiría HTML donde espera JSON.
        options.Events.OnRedirectToLogin = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
    });

builder.Services.AddAuthorization();

// ---------- Limitación de intentos ----------
//
// Complementa el bloqueo por cuenta: aquel frena la fuerza bruta contra un usuario
// concreto, este frena a un origen que prueba muchos usuarios distintos.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "desconocido",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

builder.Services.AddScoped<IUsuarioActual, UsuarioActualCookie>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o => o.SwaggerEndpoint("/swagger/v1/swagger.json", "Asistente v1"));

    // La base de desarrollo SQLite se crea sola, porque es descartable y vive en un
    // archivo local. Contra SQL Server NUNCA se toca el esquema desde la aplicación: ahí
    // los objetos se crean ejecutando los scripts de db/ a mano.
    using var scope = app.Services.CreateScope();
    var ajustes = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;

    if (string.Equals(ajustes.Asistente.Provider, "Sqlite", StringComparison.OrdinalIgnoreCase))
    {
        var db = scope.ServiceProvider.GetRequiredService<AsistenteDbContext>();
        await db.Database.EnsureCreatedAsync();
    }
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
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

namespace Asistente.Api
{
    /// <summary>Necesario para que las pruebas de integración puedan referenciar el host.</summary>
    public partial class Program;
}
