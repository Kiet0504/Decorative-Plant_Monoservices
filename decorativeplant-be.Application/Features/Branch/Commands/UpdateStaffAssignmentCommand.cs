// decorativeplant-be.Application/Features/Branch/Commands/UpdateStaffAssignmentCommand.cs

using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public record UpdateStaffAssignmentCommand : IRequest<StaffAssignmentDto>
{
    /// <summary>Branch from route; must match the assignment's branch.</summary>
    public Guid BranchId { get; init; }

    public Guid StaffAssignmentId { get; init; }
    public string Role { get; init; } = string.Empty; // branchManager, cultivationStaff, storeStaff, fulfillmentStaff
    public string? Position { get; init; }
    public bool IsPrimary { get; init; }
    public bool CanManageInventory { get; init; }
    public bool CanManageOrders { get; init; }
    public bool CanManageStaff { get; init; }
    public bool CanViewOtherBranches { get; init; }

    // Internal - populated by controller from HttpContext
    public string CurrentUserRole { get; init; } = string.Empty;
    public Guid? CurrentUserBranchId { get; init; } // For branchManager scope validation
}
