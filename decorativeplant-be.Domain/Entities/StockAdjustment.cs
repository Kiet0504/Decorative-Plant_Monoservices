using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Stock adjustment record. JSONB: meta_info. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class StockAdjustment
{
    public Guid Id { get; set; }
    public Guid? StockId { get; set; }
    public string? Type { get; set; }
    public int QuantityChange { get; set; }
    public string? Reason { get; set; }
    public JsonDocument? MetaInfo { get; set; }
    public DateTime? CreatedAt { get; set; }

    public BatchStock? Stock { get; set; }
}
