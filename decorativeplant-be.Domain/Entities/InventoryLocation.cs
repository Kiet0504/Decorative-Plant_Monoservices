using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class InventoryLocation : BaseEntity
{
    public Guid? BranchId { get; set; } // Nullable? DBML says ref > branch.id. Assume optional or mandatory? Usually location belongs to branch.
    public Branch? Branch { get; set; }
    
    public Guid? ParentLocationId { get; set; }
    public InventoryLocation? ParentLocation { get; set; }
    
    public string? Code { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }
    public JsonNode? Details { get; set; } // capacity, environment_type
}
