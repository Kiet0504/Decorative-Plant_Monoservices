using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class PaymentTransaction : BaseEntity
{
    public Guid OrderId { get; set; }
    public OrderHeader Order { get; set; } = null!;
    
    public string PaymentMethod { get; set; } = string.Empty; // e.g., CARD, WALLET
    public string TransactionCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public JsonNode? PayloadJson { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}
