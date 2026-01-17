using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class StorePlantDiagnosis : BaseEntity
{
    public Guid StockId { get; set; }
    public string? ImageUrl { get; set; }
    public JsonDocument? AiResultJson { get; set; } // AI diagnosis result
    public bool IsResolved { get; set; } = false;

    // Navigation properties
    public BatchStock BatchStock { get; set; } = null!;
}
