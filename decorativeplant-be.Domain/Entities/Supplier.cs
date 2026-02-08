using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Supplier. JSONB: contact_info. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class Supplier
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? TaxCode { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public JsonDocument? ContactInfo { get; set; }
    public bool? IsActive { get; set; }

    public ICollection<PlantBatch> PlantBatches { get; set; } = new List<PlantBatch>();
}
