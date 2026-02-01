using System.Text.Json.Nodes;

namespace decorativeplant_be.Domain.Entities;

public class StaffAssignment : BaseEntity
{
    public Guid StaffId { get; set; }
    public UserAccount Staff { get; set; } = null!;
    
    public Guid BranchId { get; set; }
    public Branch Branch { get; set; } = null!;
    
    public string? Position { get; set; }
    public bool IsPrimary { get; set; } = true;
    public JsonNode? Permissions { get; set; }
    
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
