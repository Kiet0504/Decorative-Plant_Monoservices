namespace decorativeplant_be.Domain.Entities;

public class Voucher : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;
    
    public string Code { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal DiscountValue { get; set; }
    public decimal MinOrderValue { get; set; }
    public int MaxUsage { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidTo { get; set; }
}
