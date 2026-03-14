// decorativeplant-be.Application/Features/Branch/DTOs/StaffAssignmentDto.cs

namespace decorativeplant_be.Application.Features.Branch.DTOs;

public class StaffAssignmentDto
{
    public Guid Id { get; set; }
    public Guid StaffId { get; set; }
    public string StaffEmail { get; set; } = string.Empty;
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
    public string? Position { get; set; }
    public bool IsPrimary { get; set; }
    public DateTime AssignedAt { get; set; }

    // From Permissions jsonb
    public bool CanManageInventory { get; set; }
    public bool CanManageOrders { get; set; }
    public bool CanManageStaff { get; set; }
    public bool CanViewOtherBranches { get; set; }
}
