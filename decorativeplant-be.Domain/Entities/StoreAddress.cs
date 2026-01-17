using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

public class StoreAddress : BaseEntity
{
    public Guid StoreId { get; set; }
    public string Label { get; set; } = string.Empty; // Warehouse/Showroom/ReturnPoint
    public string RecipientName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string FullAddressText { get; set; } = string.Empty;
    public string? City { get; set; }
    public JsonDocument? Coordinates { get; set; } // {lat: float, long: float}
    public bool IsDefaultPickup { get; set; } = false;

    // Navigation properties
    public Store Store { get; set; } = null!;
    public ICollection<InventoryLocation> InventoryLocations { get; set; } = new List<InventoryLocation>();
    public ICollection<PickupAddressSnapshot> PickupAddressSnapshots { get; set; } = new List<PickupAddressSnapshot>();
}
