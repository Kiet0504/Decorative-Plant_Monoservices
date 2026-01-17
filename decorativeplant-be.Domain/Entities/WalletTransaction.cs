namespace decorativeplant_be.Domain.Entities;

public class WalletTransaction : BaseEntity
{
    public Guid WalletId { get; set; }
    public decimal Amount { get; set; } // + for revenue, - for withdraw/fees
    public string Type { get; set; } = string.Empty; // OrderRevenue / PlatformFee / Withdrawal
    public Guid? RefOrderId { get; set; }
    public string? Description { get; set; }

    // Navigation properties
    public StoreWallet StoreWallet { get; set; } = null!;
    public OrderHeader? RefOrder { get; set; }
}
