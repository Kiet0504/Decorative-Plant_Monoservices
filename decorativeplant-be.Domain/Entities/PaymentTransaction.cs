using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class PaymentTransaction : BaseEntity
{
    public Guid OrderId { get; set; }
    public OrderHeader Order { get; set; } = null!;
    
    public string TransactionCode { get; set; } = string.Empty;
    public JsonNode? Details { get; set; }
}
