using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Single company record — multi-branch nursery chain.
/// JSONB: info — logo_url, website, description, address. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public JsonDocument? Info { get; set; }

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
