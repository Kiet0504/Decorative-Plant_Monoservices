using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class IoTDevice : BaseEntity
{
    public Guid? BranchId { get; set; }
    public Branch? Branch { get; set; }
    
    public Guid? LocationId { get; set; }
    public InventoryLocation? Location { get; set; }
    
    public JsonNode? DeviceInfo { get; set; } // code, name, type, mac...
    public string? Status { get; set; }
    public JsonNode? ActivityLog { get; set; }
    public JsonNode? Components { get; set; } // sensors definitions
}
