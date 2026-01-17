namespace decorativeplant_be.Domain.Entities;

public class PickupAddressSnapshot : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid StoreAddressId { get; set; }
    public string FullAddressText { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;

    // Navigation properties
    public OrderHeader OrderHeader { get; set; } = null!;
    public StoreAddress StoreAddress { get; set; } = null!;
    public Shipping? Shipping { get; set; }
}
