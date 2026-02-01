using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class ShippingZone : BaseEntity
{
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    
    public string Name { get; set; } = string.Empty;
    public JsonNode? Locations { get; set; }
    public JsonNode? FeeConfig { get; set; }
    public JsonNode? DeliveryTimeConfig { get; set; }
}
