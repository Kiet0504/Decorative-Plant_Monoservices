namespace decorativeplant_be.Domain.Entities;

public class InventoryLocation : BaseEntity
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = null!;
    
    public Guid? AddressId { get; set; }
    public StoreAddress? Address { get; set; }
    
    public Guid? ParentLocationId { get; set; }
    public InventoryLocation? ParentLocation { get; set; }
    
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}
