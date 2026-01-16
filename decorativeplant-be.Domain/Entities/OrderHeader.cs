using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class OrderHeader : BaseEntity
{
    public Guid UserId { get; set; }
    public UserAccount User { get; set; } = null!;
    
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;
    
    public Guid? VoucherId { get; set; }
    public Voucher? Voucher { get; set; }
    
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public string Status { get; set; } = string.Empty; // e.g., PENDING, PAID, SHIPPED
    public string PaymentStatus { get; set; } = string.Empty;
    public JsonNode? StatusTimelineJson { get; set; }
    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
}
