using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class Company : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? TaxCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public JsonNode? Info { get; set; } // logo_url, website, description, address

}
