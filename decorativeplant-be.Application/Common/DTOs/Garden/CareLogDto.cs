namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// Response DTO for a care log entry.
/// </summary>
public class CareLogDto
{
    public Guid Id { get; set; }

    public Guid? GardenPlantId { get; set; }

    public Guid? ScheduleId { get; set; }

    public CareLogLogInfoDto? LogInfo { get; set; }

    public List<CareLogImageDto>? Images { get; set; }

    public DateTime? PerformedAt { get; set; }
}
