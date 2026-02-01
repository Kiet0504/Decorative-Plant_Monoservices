using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class Shipping : BaseEntity
{
    public Guid OrderId { get; set; }
    public OrderHeader Order { get; set; } = null!;
    
    public string? TrackingCode { get; set; }
    public JsonNode? CarrierInfo { get; set; }
    public string? Status { get; set; }
    public JsonNode? DeliveryDetails { get; set; }
    public JsonNode? Events { get; set; }
}
