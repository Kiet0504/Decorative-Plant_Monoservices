using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

[Table("diagnosis_log")]
public class DiagnosisLog : BaseEntity
{
    public Guid GardenPlantId { get; set; }
    public MyGardenPlant GardenPlant { get; set; } = null!;

    public string? ImageUrl { get; set; }

    public JsonNode? AiResultJson { get; set; }
    public JsonNode? UserFeedbackJson { get; set; }
}
