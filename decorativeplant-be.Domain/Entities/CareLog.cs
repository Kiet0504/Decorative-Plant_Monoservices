using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class CareLog : BaseEntity
{
    public Guid GardenPlantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public JsonDocument? ImagesJson { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public MyGardenPlant MyGardenPlant { get; set; } = null!;
}
