using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using decorativeplant_be.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Infrastructure.Data.Repositories;

/// <summary>
/// Repository implementation for Garden entities. Uses ApplicationDbContext directly
/// since GardenPlant, CareLog, PlantDiagnosis do not inherit BaseEntity.
/// </summary>
public class GardenRepository : IGardenRepository
{
    private readonly ApplicationDbContext _context;

    public GardenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GardenPlant?> GetPlantByIdAsync(Guid id, bool includeTaxonomy = false, CancellationToken cancellationToken = default)
    {
        var query = _context.GardenPlants.AsQueryable();

        if (includeTaxonomy)
        {
            query = query.Include(g => g.Taxonomy);
        }

        return await query.FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<(IEnumerable<GardenPlant> Items, int TotalCount)> GetPlantsByUserIdAsync(
        Guid userId,
        bool includeArchived = false,
        string? healthFilter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _context.GardenPlants
            .Where(g => g.UserId == userId)
            .Include(g => g.Taxonomy)
            .AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(g => !g.IsArchived);
        }

        if (!string.IsNullOrEmpty(healthFilter))
        {
            var filterJson = $"{{\"health\":\"{healthFilter}\"}}";
            query = query.Where(g => g.Details != null && EF.Functions.JsonContains(g.Details, filterJson));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<GardenPlant> AddPlantAsync(GardenPlant plant, CancellationToken cancellationToken = default)
    {
        plant.CreatedAt ??= DateTime.UtcNow;
        await _context.GardenPlants.AddAsync(plant, cancellationToken);
        return plant;
    }

    public Task UpdatePlantAsync(GardenPlant plant, CancellationToken cancellationToken = default)
    {
        _context.GardenPlants.Update(plant);
        return Task.CompletedTask;
    }

    public Task DeletePlantAsync(GardenPlant plant, CancellationToken cancellationToken = default)
    {
        _context.GardenPlants.Remove(plant);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<CareLog>> GetCareLogsByPlantIdAsync(
        Guid gardenPlantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.CareLogs
            .Where(c => c.GardenPlantId == gardenPlantId)
            .OrderByDescending(c => c.PerformedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CareLog>> GetRecentCareLogsByPlantIdAsync(
        Guid gardenPlantId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return Array.Empty<CareLog>();

        return await _context.CareLogs
            .Where(c => c.GardenPlantId == gardenPlantId)
            .OrderByDescending(c => c.PerformedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IEnumerable<CareLog> Items, int TotalCount)> GetPhotoCareLogsByPlantIdAsync(
        Guid gardenPlantId,
        DateTime? before,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0) return (Array.Empty<CareLog>(), 0);

        var query = _context.CareLogs
            .Where(c => c.GardenPlantId == gardenPlantId && c.Images != null);

        if (before.HasValue)
        {
            query = query.Where(c => c.PerformedAt != null && c.PerformedAt < before.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Fetch newest first for pagination, caller can sort as needed.
        var items = await query
            .OrderByDescending(c => c.PerformedAt)
            .ThenByDescending(c => c.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        // Ensure non-empty images array (filter in-memory; JSONB array length isn't easily filterable here)
        var filtered = items.Where(c =>
        {
            try
            {
                return c.Images != null
                    && c.Images.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array
                    && c.Images.RootElement.GetArrayLength() > 0;
            }
            catch
            {
                return false;
            }
        }).ToList();

        return (filtered, totalCount);
    }

    public async Task<CareLog?> GetCareLogByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CareLogs
            .Include(c => c.GardenPlant)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<CareLog> AddCareLogAsync(CareLog careLog, CancellationToken cancellationToken = default)
    {
        await _context.CareLogs.AddAsync(careLog, cancellationToken);
        return careLog;
    }

    public async Task<IEnumerable<CareSchedule>> GetSchedulesByPlantIdAsync(
        Guid gardenPlantId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.CareSchedules.Where(s => s.GardenPlantId == gardenPlantId);
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<CareSchedule?> GetScheduleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.CareSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    public async Task<CareSchedule> AddScheduleAsync(CareSchedule schedule, CancellationToken cancellationToken = default)
    {
        await _context.CareSchedules.AddAsync(schedule, cancellationToken);
        return schedule;
    }

    public Task UpdateScheduleAsync(CareSchedule schedule, CancellationToken cancellationToken = default)
    {
        _context.CareSchedules.Update(schedule);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<PlantDiagnosis>> GetPlantDiagnosesByPlantIdAsync(
        Guid gardenPlantId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PlantDiagnoses
            .Where(p => p.GardenPlantId == gardenPlantId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<PlantDiagnosis> AddPlantDiagnosisAsync(PlantDiagnosis diagnosis, CancellationToken cancellationToken = default)
    {
        diagnosis.CreatedAt ??= DateTime.UtcNow;
        await _context.PlantDiagnoses.AddAsync(diagnosis, cancellationToken);
        return diagnosis;
    }

    public async Task<PlantDiagnosis?> GetPlantDiagnosisByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.PlantDiagnoses
            .Include(p => p.GardenPlant)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public Task UpdatePlantDiagnosisAsync(PlantDiagnosis diagnosis, CancellationToken cancellationToken = default)
    {
        _context.PlantDiagnoses.Update(diagnosis);
        return Task.CompletedTask;
    }

    public async Task<(IEnumerable<PlantDiagnosis> Items, int TotalCount)> GetDiagnosesByUserIdAsync(
        Guid userId,
        Guid? gardenPlantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<PlantDiagnosis> query;

        if (gardenPlantId.HasValue)
        {
            var plant = await _context.GardenPlants.FindAsync([gardenPlantId.Value], cancellationToken);
            if (plant == null || plant.UserId != userId)
            {
                return (Enumerable.Empty<PlantDiagnosis>(), 0);
            }
            query = _context.PlantDiagnoses.Where(p => p.GardenPlantId == gardenPlantId.Value);
        }
        else
        {
            var plantIdsForUser = _context.GardenPlants
                .Where(g => g.UserId == userId)
                .Select(g => g.Id);
            query = _context.PlantDiagnoses
                .Where(p => p.UserId == userId || (p.GardenPlantId != null && plantIdsForUser.Contains(p.GardenPlantId!.Value)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(p => p.GardenPlant)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
