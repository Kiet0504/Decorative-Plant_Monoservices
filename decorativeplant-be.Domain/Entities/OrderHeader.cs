using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class OrderHeader : BaseEntity
{
    public string OrderCode { get; set; } = string.Empty;
    
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    public JsonNode? TypeInfo { get; set; }
    public JsonNode? Financials { get; set; }
    public string? Status { get; set; }
    public JsonNode? Notes { get; set; }
    
    public JsonNode? DeliveryAddress { get; set; }
    public JsonNode? PickupInfo { get; set; }
    
    public DateTime? ConfirmedAt { get; set; }
}
