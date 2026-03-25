using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Repository for Garden entities (GardenPlant, CareLog, PlantDiagnosis).
/// Used because these entities do not inherit BaseEntity and cannot use IRepositoryFactory.
/// </summary>
public interface IGardenRepository
{
    Task<GardenPlant?> GetPlantByIdAsync(Guid id, bool includeTaxonomy = false, CancellationToken cancellationToken = default);

    Task<(IEnumerable<GardenPlant> Items, int TotalCount)> GetPlantsByUserIdAsync(
        Guid userId,
        bool includeArchived = false,
        string? healthFilter = null,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<GardenPlant> AddPlantAsync(GardenPlant plant, CancellationToken cancellationToken = default);

    Task UpdatePlantAsync(GardenPlant plant, CancellationToken cancellationToken = default);

    Task DeletePlantAsync(GardenPlant plant, CancellationToken cancellationToken = default);

    Task<IEnumerable<CareLog>> GetCareLogsByPlantIdAsync(
        Guid gardenPlantId,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<CareLog>> GetRecentCareLogsByPlantIdAsync(
        Guid gardenPlantId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<(IEnumerable<CareLog> Items, int TotalCount)> GetPhotoCareLogsByPlantIdAsync(
        Guid gardenPlantId,
        DateTime? before,
        int limit,
        CancellationToken cancellationToken = default);

    Task<CareLog?> GetCareLogByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CareLog> AddCareLogAsync(CareLog careLog, CancellationToken cancellationToken = default);

    Task<IEnumerable<CareSchedule>> GetSchedulesByPlantIdAsync(
        Guid gardenPlantId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<PlantDiagnosis>> GetPlantDiagnosesByPlantIdAsync(
        Guid gardenPlantId,
        CancellationToken cancellationToken = default);

    Task<PlantDiagnosis> AddPlantDiagnosisAsync(PlantDiagnosis diagnosis, CancellationToken cancellationToken = default);

    Task<PlantDiagnosis?> GetPlantDiagnosisByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task UpdatePlantDiagnosisAsync(PlantDiagnosis diagnosis, CancellationToken cancellationToken = default);

    Task<(IEnumerable<PlantDiagnosis> Items, int TotalCount)> GetDiagnosesByUserIdAsync(
        Guid userId,
        Guid? gardenPlantId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
}
