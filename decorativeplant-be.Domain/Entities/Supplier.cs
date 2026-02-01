using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class Supplier : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public JsonNode? ContactInfo { get; set; }
    public bool? IsActive { get; set; }
}
