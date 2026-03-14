// decorativeplant-be.Application/Features/Branch/Commands/UnassignStaffFromBranchCommand.cs

using MediatR;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public record UnassignStaffFromBranchCommand : IRequest<Unit>
{
    public Guid StaffAssignmentId { get; init; }

    // Internal - populated by controller from HttpContext
    public string CurrentUserRole { get; init; } = string.Empty;
    public Guid? CurrentUserId { get; init; }
}
