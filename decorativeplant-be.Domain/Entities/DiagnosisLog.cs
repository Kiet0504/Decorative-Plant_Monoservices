using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class DiagnosisLog : BaseEntity
{
    public Guid GardenPlantId { get; set; }
    public string? ImageUrl { get; set; }
    public JsonDocument? AiResultJson { get; set; } // AI diagnosis result
    public JsonDocument? UserFeedbackJson { get; set; }

    // Navigation properties
    public MyGardenPlant MyGardenPlant { get; set; } = null!;
}
