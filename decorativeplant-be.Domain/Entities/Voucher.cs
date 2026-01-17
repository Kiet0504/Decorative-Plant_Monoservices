namespace decorativeplant_be.Domain.Entities;

public class Voucher : BaseEntity
{
    public Guid? StoreId { get; set; } // NULL = platform-wide voucher
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty; // Percent/Fixed
    public decimal DiscountValue { get; set; }
    public decimal MinOrderValue { get; set; }
    public int? MaxUsage { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }

    // Navigation properties
    public Store? Store { get; set; }
}
