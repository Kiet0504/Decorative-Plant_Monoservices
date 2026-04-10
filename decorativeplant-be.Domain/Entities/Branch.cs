using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Branch (nursery/showroom/warehouse). JSONB: contact_info, operating_hours, settings.
/// See docs/JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class Branch : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? BranchType { get; set; }
    public double? Lat { get; set; }
    public double? Long { get; set; }
    public JsonDocument? ContactInfo { get; set; }
    public JsonDocument? OperatingHours { get; set; }
    public JsonDocument? Settings { get; set; }
    public bool IsActive { get; set; } = true;

    public Company Company { get; set; } = null!;
    public ICollection<StaffAssignment> StaffAssignments { get; set; } = new List<StaffAssignment>();
    public ICollection<InventoryLocation> InventoryLocations { get; set; } = new List<InventoryLocation>();
    public ICollection<PlantBatch> PlantBatches { get; set; } = new List<PlantBatch>();
    public ICollection<IotDevice> IotDevices { get; set; } = new List<IotDevice>();
    public ICollection<ProductListing> ProductListings { get; set; } = new List<ProductListing>();
    public ICollection<ShippingZone> ShippingZones { get; set; } = new List<ShippingZone>();
    public ICollection<Voucher> Vouchers { get; set; } = new List<Voucher>();
    public ICollection<Promotion> Promotions { get; set; } = new List<Promotion>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<StockTransfer> StockTransfersFrom { get; set; } = new List<StockTransfer>();
    public ICollection<StockTransfer> StockTransfersTo { get; set; } = new List<StockTransfer>();
}
