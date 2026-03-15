// decorativeplant-be.Application/Features/Branch/Handlers/UpdateStaffAssignmentCommandHandler.cs

using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.Commands;
using decorativeplant_be.Application.Features.Branch.DTOs;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class UpdateStaffAssignmentCommandHandler : IRequestHandler<UpdateStaffAssignmentCommand, StaffAssignmentDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateStaffAssignmentCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StaffAssignmentDto> Handle(UpdateStaffAssignmentCommand request, CancellationToken cancellationToken)
    {
        // 1. Find + Include Staff + Include Branch
        var staffAssignment = await _context.StaffAssignments
            .Include(sa => sa.Staff)
            .Include(sa => sa.Branch)
            .FirstOrDefaultAsync(sa => sa.Id == request.StaffAssignmentId, cancellationToken);

        if (staffAssignment == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.StaffAssignment), request.StaffAssignmentId);
        }

        // 1b. Validate branchManager can only update staff within their own branch
        if (request.CurrentUserRole == "branchManager")
        {
            if (!request.CurrentUserBranchId.HasValue || staffAssignment.BranchId != request.CurrentUserBranchId.Value)
            {
                throw new InvalidOperationException(
                    "Branch managers can only update staff assignments within their own branch.");
            }
        }

        // 1c. Validate role assignment permissions
        ValidateRoleAssignmentPermissions(request.CurrentUserRole, request.Role);

        // 1d. Update UserAccount.Role if role is being changed
        if (!string.IsNullOrEmpty(request.Role) && staffAssignment.Staff.Role != request.Role)
        {
            staffAssignment.Staff.Role = request.Role;
            staffAssignment.Staff.UpdatedAt = DateTime.UtcNow;
        }

        // 2. If IsPrimary=true → reset other primary assignments for same staff
        if (request.IsPrimary && !staffAssignment.IsPrimary)
        {
            var otherPrimaryAssignments = await _context.StaffAssignments
                .Where(sa => sa.StaffId == staffAssignment.StaffId && sa.IsPrimary && sa.Id != request.StaffAssignmentId)
                .ToListAsync(cancellationToken);

            foreach (var assignment in otherPrimaryAssignments)
            {
                assignment.IsPrimary = false;
            }
        }

        // 3. Update Position, IsPrimary
        staffAssignment.Position = request.Position;
        staffAssignment.IsPrimary = request.IsPrimary;

        // 4. Rebuild Permissions JsonDocument entirely
        staffAssignment.Permissions = JsonSerializer.SerializeToDocument(new
        {
            can_manage_inventory = request.CanManageInventory,
            can_manage_orders = request.CanManageOrders,
            can_manage_staff = request.CanManageStaff,
            can_view_other_branches = request.CanViewOtherBranches
        });

        // 5. SaveChangesAsync → return ToDto
        await _context.SaveChangesAsync(cancellationToken);

        return staffAssignment.ToDto(staffAssignment.Staff.Email, staffAssignment.Branch.Name);
    }

    private static void ValidateRoleAssignmentPermissions(string currentUserRole, string roleToAssign)
    {
        // Admin can assign ALL roles
        if (currentUserRole == "admin")
        {
            return; // Admin has full permissions
        }

        // BranchManager can assign ONLY staff roles (NOT branchManager)
        if (currentUserRole == "branchManager")
        {
            var allowedRoles = new[] { "cultivationStaff", "storeStaff", "fulfillmentStaff" };

            if (!allowedRoles.Contains(roleToAssign))
            {
                throw new InvalidOperationException(
                    $"Branch managers can only assign staff roles (cultivationStaff, storeStaff, fulfillmentStaff). " +
                    $"Cannot assign role '{roleToAssign}'.");
            }

            return; // BranchManager has permission for staff roles
        }

        // Any other role cannot assign staff
        throw new InvalidOperationException(
            $"Role '{currentUserRole}' does not have permission to assign staff to branches.");
    }
}
