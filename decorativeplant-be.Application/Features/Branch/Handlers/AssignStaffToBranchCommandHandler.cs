// decorativeplant-be.Application/Features/Branch/Handlers/AssignStaffToBranchCommandHandler.cs

using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Branch.Commands;
using decorativeplant_be.Application.Features.Branch.DTOs;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Application.Features.Branch.Handlers;

public class AssignStaffToBranchCommandHandler : IRequestHandler<AssignStaffToBranchCommand, StaffAssignmentDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<AssignStaffToBranchCommandHandler> _logger;

    public AssignStaffToBranchCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        ILogger<AssignStaffToBranchCommandHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<StaffAssignmentDto> Handle(AssignStaffToBranchCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify Branch exists + IsActive
        var branch = await _context.Branches
            .FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);

        if (branch == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Branch), request.BranchId);
        }

        if (!branch.IsActive)
        {
            throw new InvalidOperationException($"Cannot assign staff to inactive branch '{branch.Name}'.");
        }

        // 2. Verify UserAccount (StaffId) exists
        var staff = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.Id == request.StaffId, cancellationToken);

        if (staff == null)
        {
            throw new NotFoundException(nameof(UserAccount), request.StaffId);
        }

        // 2b. Validate branch_manager can only assign staff to their own branch
        if (StaffRoleNormalizer.IsBranchManager(request.CurrentUserRole))
        {
            if (!request.CurrentUserBranchId.HasValue || branch.Id != request.CurrentUserBranchId.Value)
            {
                throw new InvalidOperationException(
                    "Branch managers can only assign staff to their own branch.");
            }
        }

        var roleToStore = StaffRoleNormalizer.Normalize(request.Role);
        var currentUserRoleNorm = StaffRoleNormalizer.Normalize(request.CurrentUserRole);

        // 2c. Validate role assignment permissions
        ValidateRoleAssignmentPermissions(currentUserRoleNorm, roleToStore);

        // Update UserAccount.Role
        staff.Role = roleToStore;
        staff.UpdatedAt = DateTime.UtcNow;

        // 3. Check duplicate StaffId+BranchId
        var existingAssignment = await _context.StaffAssignments
            .AnyAsync(sa => sa.StaffId == request.StaffId && sa.BranchId == request.BranchId, cancellationToken);

        if (existingAssignment)
        {
            throw new InvalidOperationException($"Staff '{staff.Email}' is already assigned to branch '{branch.Name}'.");
        }

        // 4. If IsPrimary=true → reset other primary for same staff
        if (request.IsPrimary)
        {
            var otherPrimaryAssignments = await _context.StaffAssignments
                .Where(sa => sa.StaffId == request.StaffId && sa.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var assignment in otherPrimaryAssignments)
            {
                assignment.IsPrimary = false;
            }
        }

        // 5. Create StaffAssignment
        var staffAssignment = new StaffAssignment
        {
            Id = Guid.NewGuid(),
            StaffId = request.StaffId,
            BranchId = request.BranchId,
            Position = request.Position,
            IsPrimary = request.IsPrimary,
            Permissions = JsonSerializer.SerializeToDocument(new
            {
                can_manage_inventory = request.CanManageInventory,
                can_manage_orders = request.CanManageOrders,
                can_manage_staff = request.CanManageStaff,
                can_view_other_branches = request.CanViewOtherBranches
            }),
            AssignedAt = DateTime.UtcNow
        };

        _context.StaffAssignments.Add(staffAssignment);
        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await StaffAssignmentEmailNotifier.SendStaffAssignedAsync(
                _emailService,
                staff.Email,
                staff.DisplayName,
                branch.Name,
                roleToStore,
                temporaryPasswordPlaintext: null,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send staff assignment email to {Email}", staff.Email);
        }

        // 6. Return ToDto
        return staffAssignment.ToDto(staff.Email, branch.Name);
    }

    private static void ValidateRoleAssignmentPermissions(string currentUserRoleNorm, string roleToAssignNorm)
    {
        if (currentUserRoleNorm == "admin")
            return;

        if (StaffRoleNormalizer.IsBranchManager(currentUserRoleNorm))
        {
            var allowed = new[] { "cultivation_staff", "store_staff", "fulfillment_staff" };

            if (!allowed.Contains(roleToAssignNorm))
            {
                throw new InvalidOperationException(
                    "Branch managers can only assign cultivation_staff, store_staff, or fulfillment_staff. " +
                    $"Cannot assign role '{roleToAssignNorm}'.");
            }

            return;
        }

        throw new InvalidOperationException(
            $"Role '{currentUserRoleNorm}' does not have permission to assign staff to branches.");
    }
}
