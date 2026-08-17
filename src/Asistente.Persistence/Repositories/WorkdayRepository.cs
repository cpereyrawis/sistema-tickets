using Asistente.Domain.Entities;
using Asistente.Domain.Services.Interfaces;
using Asistente.Persistence.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Asistente.Persistence.Repositories;

/// <summary>
/// Acceso a la jornada sobre EF Core.
///
/// Carga siempre el agregado completo (sesiones, eventos y auditoría): las reglas de
/// §6.1 miran los tramos existentes para detectar solapamientos, así que una carga
/// parcial haría que el dominio decidiera con información incompleta.
/// </summary>
public sealed class WorkdayRepository : IWorkdayRepository
{
    private readonly AsistenteDbContext _db;
    private readonly ILogger<WorkdayRepository> _logger;

    public WorkdayRepository(AsistenteDbContext db, ILogger<WorkdayRepository> logger)
    {
        _db = db;
        _logger = logger;
    }

    public Task<Workday?> ObtenerAbiertaAsync(long userId, CancellationToken ct) =>
        Consulta()
            .Where(j => j.UserId == userId)
            .Where(j => j.Estado == EstadoJornada.Activa || j.Estado == EstadoJornada.EnDescanso)
            .OrderByDescending(j => j.InicioUtc)
            .FirstOrDefaultAsync(ct);

    public Task<Workday?> ObtenerVigenteAsync(long userId, CancellationToken ct) =>
        Consulta()
            .Where(j => j.UserId == userId)
            .OrderByDescending(j => j.InicioUtc)
            .FirstOrDefaultAsync(ct);

    public async Task AgregarAsync(Workday jornada, CancellationToken ct) =>
        await _db.Jornadas.AddAsync(jornada, ct);

    public async Task<bool> GuardarAsync(CancellationToken ct)
    {
        // Incrementar la versión en cada confirmación es lo que hace efectiva la
        // concurrencia optimista: si otro proceso guardó primero, el UPDATE no encuentra
        // la fila con la versión esperada y EF lanza DbUpdateConcurrencyException.
        foreach (var entrada in _db.ChangeTracker.Entries<Workday>())
        {
            if (entrada.State is EntityState.Modified or EntityState.Added)
            {
                entrada.CurrentValues[nameof(Workday.Version)] =
                    entrada.OriginalValues.GetValue<long>(nameof(Workday.Version)) + 1;
            }
        }

        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Conflicto de concurrencia al guardar la jornada");
            return false;
        }
    }

    private IQueryable<Workday> Consulta() =>
        _db.Jornadas
            .Include(j => j.Sesiones)
            .Include(j => j.Eventos)
            .Include(j => j.Auditoria);
}
