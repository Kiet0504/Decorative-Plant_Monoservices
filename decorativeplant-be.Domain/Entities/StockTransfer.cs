using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Transfer between branches. JSONB: logistics_info. See JSONB_SCHEMA_REFERENCE.md.
/// PostgreSQL uuid[] mapped as <see cref="DeliveryStaffIds"/> (minimum two fulfillment staff before shipping).
/// </summary>
public class StockTransfer
{
    public Guid Id { get; set; }
    public string? TransferCode { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? FromBranchId { get; set; }
    public Guid? ToBranchId { get; set; }
    public Guid? FromLocationId { get; set; }
    public Guid? ToLocationId { get; set; }
    public int Quantity { get; set; }
    public string? Status { get; set; }
    public List<Guid>? DeliveryStaffIds { get; set; }
    public JsonDocument? LogisticsInfo { get; set; }
    public DateTime? CreatedAt { get; set; }

    public PlantBatch? Batch { get; set; }
    public Branch? FromBranch { get; set; }
    public Branch? ToBranch { get; set; }
    public InventoryLocation? FromLocation { get; set; }
    public InventoryLocation? ToLocation { get; set; }
}
