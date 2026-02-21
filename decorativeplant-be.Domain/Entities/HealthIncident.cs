using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Health incident (disease/pest etc). JSONB: details, treatment_info, status_info, images.
/// See docs/JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class HealthIncident
{
    public Guid Id { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? StockId { get; set; }
    public string? IncidentType { get; set; }
    public string? Severity { get; set; }
    public string? Description { get; set; }
    public JsonDocument? Details { get; set; }
    public JsonDocument? TreatmentInfo { get; set; }
    public JsonDocument? StatusInfo { get; set; }
    public JsonDocument? Images { get; set; }

    public PlantBatch? Batch { get; set; }
    public BatchStock? Stock { get; set; }
}
