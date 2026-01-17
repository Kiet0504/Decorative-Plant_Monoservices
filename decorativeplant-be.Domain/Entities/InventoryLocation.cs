namespace decorativeplant_be.Domain.Entities;

public class InventoryLocation : BaseEntity
{
    public Guid StoreId { get; set; }
    public Guid? AddressId { get; set; } // Which warehouse this location belongs to
    public Guid? ParentLocationId { get; set; } // Supports hierarchy: Greenhouse A -> Shelf 1 -> Tray 5
    public string Name { get; set; } = string.Empty; // e.g., "Tray 05"
    public string Type { get; set; } = string.Empty; // Room/Shelf/Tray/Outdoor

    // Navigation properties
    public Store Store { get; set; } = null!;
    public StoreAddress? Address { get; set; }
    public InventoryLocation? ParentLocation { get; set; }
    public ICollection<InventoryLocation> ChildLocations { get; set; } = new List<InventoryLocation>();
    public ICollection<BatchStock> BatchStocks { get; set; } = new List<BatchStock>();
    public ICollection<IotDevice> IotDevices { get; set; } = new List<IotDevice>();
}
