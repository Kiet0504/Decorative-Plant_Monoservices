// decorativeplant-be.Application/Features/Branch/Commands/AssignStaffToBranchCommand.cs

using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public record AssignStaffToBranchCommand : IRequest<StaffAssignmentDto>
{
    public Guid StaffId { get; init; }
    public Guid BranchId { get; init; }
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
