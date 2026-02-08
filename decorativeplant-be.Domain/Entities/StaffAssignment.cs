using System.Text.Json;

namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Staff assigned to branch. JSONB: permissions. See JSONB_SCHEMA_REFERENCE.md
/// </summary>
public class StaffAssignment
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public Guid BranchId { get; set; }
    public string? Position { get; set; }
    public bool IsPrimary { get; set; } = true;
    public JsonDocument? Permissions { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public UserAccount Staff { get; set; } = null!;
    public Branch Branch { get; set; } = null!;
}
