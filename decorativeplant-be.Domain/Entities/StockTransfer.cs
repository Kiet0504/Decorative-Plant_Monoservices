using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class StockTransfer : BaseEntity
{
    public string? TransferCode { get; set; }
    
    public Guid BatchId { get; set; }
    public PlantBatch Batch { get; set; } = null!;
    
    public Guid FromBranchId { get; set; }
    public Branch FromBranch { get; set; } = null!;
    
    public Guid ToBranchId { get; set; }
    public Branch ToBranch { get; set; } = null!;
    
    public Guid FromLocationId { get; set; }
    public InventoryLocation FromLocation { get; set; } = null!;
    
    public Guid ToLocationId { get; set; }
    public InventoryLocation ToLocation { get; set; } = null!;
    
    public int Quantity { get; set; }
    public string? Status { get; set; }
    public JsonNode? LogisticsInfo { get; set; }
}
