using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Care schedule for garden plant. JSONB: task_info. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class CareSchedule
{
    public Guid Id { get; set; }
    public Guid? GardenPlantId { get; set; }
    public JsonDocument? TaskInfo { get; set; }
    public bool IsActive { get; set; }

    public GardenPlant? GardenPlant { get; set; }
    public ICollection<CareLog> CareLogs { get; set; } = new List<CareLog>();
}
