// decorativeplant-be.Application/Features/Branch/Handlers/UnassignStaffFromBranchCommandHandler.cs

using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class UnassignStaffFromBranchCommandHandler : IRequestHandler<UnassignStaffFromBranchCommand, Unit>
{
    private readonly IApplicationDbContext _context;

    public UnassignStaffFromBranchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Unit> Handle(UnassignStaffFromBranchCommand request, CancellationToken cancellationToken)
    {
        // 1. Find the staff assignment with staff included
        var staffAssignment = await _context.StaffAssignments
            .Include(sa => sa.Staff)
            .FirstOrDefaultAsync(sa => sa.Id == request.StaffAssignmentId, cancellationToken);

        if (staffAssignment == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.StaffAssignment), request.StaffAssignmentId);
        }

        // 1b. Prevent branchManager from deleting themselves
        if (request.CurrentUserRole == "branchManager")
        {
            // BranchManager cannot delete their own assignment
            if (request.CurrentUserId.HasValue && staffAssignment.StaffId == request.CurrentUserId.Value)
            {
                throw new InvalidOperationException(
                    "Branch managers cannot delete their own staff assignment. Only administrators can remove branch managers.");
            }

            // BranchManager cannot delete other branchManagers
            if (staffAssignment.Staff.Role == "branchManager")
            {
                throw new InvalidOperationException(
                    "Branch managers cannot delete other branch managers. Only administrators can remove branch managers.");
            }
        }

        var staffId = staffAssignment.StaffId;

        // 2. Remove the staff assignment
        _context.StaffAssignments.Remove(staffAssignment);

        // 3. Check if the staff has any other active assignments
        var hasOtherAssignments = await _context.StaffAssignments
            .AnyAsync(sa => sa.StaffId == staffId && sa.Id != request.StaffAssignmentId, cancellationToken);

        // 4. If no other assignments, change role to "customer"
        if (!hasOtherAssignments)
        {
            var staff = staffAssignment.Staff;
            staff.Role = "customer";
            staff.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
