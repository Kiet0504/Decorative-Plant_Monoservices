using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Physical location within a branch. JSONB: details (capacity, environment_type, description).
/// </summary>
public class InventoryLocation
{
    public Guid Id { get; set; }
    public Guid? BranchId { get; set; }
    public Guid? ParentLocationId { get; set; }
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public JsonDocument? Details { get; set; }

    public Branch? Branch { get; set; }
    public InventoryLocation? ParentLocation { get; set; }
    public ICollection<InventoryLocation> ChildLocations { get; set; } = new List<InventoryLocation>();
    public ICollection<BatchStock> BatchStocks { get; set; } = new List<BatchStock>();
    public ICollection<CultivationLog> CultivationLogs { get; set; } = new List<CultivationLog>();
    public ICollection<IotDevice> IotDevices { get; set; } = new List<IotDevice>();
    public ICollection<StockTransfer> TransfersFrom { get; set; } = new List<StockTransfer>();
    public ICollection<StockTransfer> TransfersTo { get; set; } = new List<StockTransfer>();
}
