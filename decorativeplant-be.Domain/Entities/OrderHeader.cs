namespace decorativeplant_be.Domain.Entities;

public class OrderHeader : BaseEntity
{
    public string OrderCode { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public Guid StoreId { get; set; }
    public string Status { get; set; } = string.Empty; // Pending/Paid/Processing/Completed/Cancelled
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal ShippingFee { get; set; } // Sum of child shipping fees
    public decimal DiscountAmount { get; set; }
    public string? BuyerNote { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public UserAccount UserAccount { get; set; } = null!;
    public Store Store { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();
    public Shipping? Shipping { get; set; }
    public PickupAddressSnapshot? PickupAddressSnapshot { get; set; }
    public ShippingAddressSnapshot? ShippingAddressSnapshot { get; set; }
    public ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();
    public ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
