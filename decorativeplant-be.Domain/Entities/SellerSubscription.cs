namespace decorativeplant_be.Domain.Entities;

public class SellerSubscription : BaseEntity
{
    public Guid StoreId { get; set; }
    public Guid PackageId { get; set; }
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public string Status { get; set; } = string.Empty; // Active/Expired/Cancelled
    public Guid? PaymentTransactionId { get; set; }

    // Navigation properties
    public Store Store { get; set; } = null!;
    public SellerPackage SellerPackage { get; set; } = null!;
    public PaymentTransaction? PaymentTransaction { get; set; }
}
