using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// AI plant diagnosis. JSONB: user_input, ai_result, feedback. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class PlantDiagnosis
{
    public Guid Id { get; set; }
    public Guid? GardenPlantId { get; set; }
    public JsonDocument? UserInput { get; set; }
    public JsonDocument? AiResult { get; set; }
    public JsonDocument? Feedback { get; set; }
    public DateTime? CreatedAt { get; set; }

    public GardenPlant? GardenPlant { get; set; }
}
