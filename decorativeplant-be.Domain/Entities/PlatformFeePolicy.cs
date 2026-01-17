namespace decorativeplant_be.Domain.Entities;

public class PlatformFeePolicy : BaseEntity
{
    public string Name { get; set; } = string.Empty; // Standard Commission / Premium Package Fee
    public string FeeType { get; set; } = string.Empty; // Percentage / FixedAmount
    public decimal Value { get; set; } // 2.5% or 5000 VND
    public string ApplyScope { get; set; } = string.Empty; // Order / Item / Monthly
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<SellerPackage> SellerPackages { get; set; } = new List<SellerPackage>();
}
