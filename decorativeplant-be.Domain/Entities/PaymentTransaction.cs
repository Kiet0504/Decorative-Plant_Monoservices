namespace decorativeplant_be.Domain.Entities;

public class PaymentTransaction : BaseEntity
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public string Provider { get; set; } = string.Empty; // Momo/VNPay/Stripe
    public string Type { get; set; } = string.Empty; // Payment/Refund
    public string? TransactionRef { get; set; }
    public string Status { get; set; } = string.Empty;

    // Navigation properties
    public OrderHeader OrderHeader { get; set; } = null!;
    public ICollection<SellerSubscription> SellerSubscriptions { get; set; } = new List<SellerSubscription>();
}
