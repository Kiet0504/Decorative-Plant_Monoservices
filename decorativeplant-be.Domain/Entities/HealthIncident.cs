using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class HealthIncident : BaseEntity
{
    public Guid BatchId { get; set; }
    public PlantBatch Batch { get; set; } = null!;
    
    public Guid StockId { get; set; }
    public BatchStock Stock { get; set; } = null!;
    
    public string? IncidentType { get; set; }
    public string? Severity { get; set; }
    public string? Description { get; set; }
    public JsonNode? Details { get; set; }
    public JsonNode? TreatmentInfo { get; set; }
    public JsonNode? StatusInfo { get; set; }
    public JsonNode? Images { get; set; }
}
