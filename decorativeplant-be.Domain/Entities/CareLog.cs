using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Care log for garden plant. JSONB: log_info, images. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class CareLog
{
    public Guid Id { get; set; }
    public Guid? GardenPlantId { get; set; }
    public Guid? ScheduleId { get; set; }
    public JsonDocument? LogInfo { get; set; }
    public JsonDocument? Images { get; set; }
    public DateTime? PerformedAt { get; set; }

    public GardenPlant? GardenPlant { get; set; }
    public CareSchedule? Schedule { get; set; }
}
