using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class Shipping : BaseEntity
{
    public Guid OrderId { get; set; }
    public OrderHeader Order { get; set; } = null!;
    
    public string CarrierName { get; set; } = string.Empty;
    public string TrackingCode { get; set; } = string.Empty;
    public decimal ShippingCost { get; set; }
    public DateTime EstimatedDependency { get; set; } // Matches ERD probably EstimatedDelivery
    public string Status { get; set; } = string.Empty;
    public JsonNode? EventsJson { get; set; }
}
