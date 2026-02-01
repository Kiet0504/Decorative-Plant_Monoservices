using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class PlantDiagnosis : BaseEntity
{
    public Guid GardenPlantId { get; set; }
    public GardenPlant GardenPlant { get; set; } = null!;
    
    public JsonNode? UserInput { get; set; }
    public JsonNode? AiResult { get; set; }
    public JsonNode? Feedback { get; set; }
}
