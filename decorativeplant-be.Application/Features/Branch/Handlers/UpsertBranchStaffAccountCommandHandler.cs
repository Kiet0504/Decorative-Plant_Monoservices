using System.Security.Cryptography;
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

public class UpsertBranchStaffAccountCommandHandler : IRequestHandler<UpsertBranchStaffAccountCommand, BranchStaffAccountUserDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<UpsertBranchStaffAccountCommandHandler> _logger;

    public UpsertBranchStaffAccountCommandHandler(
        IApplicationDbContext context,
        IEmailService emailService,
        IPasswordService passwordService,
        ILogger<UpsertBranchStaffAccountCommandHandler> logger)
    {
        _context = context;
        _emailService = emailService;
        _passwordService = passwordService;
        _logger = logger;
    }

    public async Task<BranchStaffAccountUserDto> Handle(UpsertBranchStaffAccountCommand request, CancellationToken cancellationToken)
    {
        var currentUserRoleNorm = StaffRoleNormalizer.Normalize(request.CurrentUserRole);
        var roleNorm = StaffRoleNormalizer.Normalize(request.Role);

        if (currentUserRoleNorm != "admin")
        {
            if (!StaffRoleNormalizer.IsBranchManager(request.CurrentUserRole))
            {
                throw new InvalidOperationException("Only administrators or branch managers can manage branch staff accounts.");
            }

            if (!request.CurrentUserBranchId.HasValue || request.BranchId != request.CurrentUserBranchId.Value)
            {
                throw new InvalidOperationException("Branch managers can only manage staff for their own branch.");
            }

            var allowedForManager = new[] { "cultivation_staff", "store_staff", "fulfillment_staff" };
            if (!allowedForManager.Contains(roleNorm))
            {
                throw new InvalidOperationException(
                    "Branch managers may only assign roles: cultivation_staff, store_staff, or fulfillment_staff.");
            }
        }

        if (roleNorm == "admin")
        {
            throw new InvalidOperationException("Cannot assign administrator role from this endpoint.");
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        if (branch == null)
        {
            throw new NotFoundException(nameof(Domain.Entities.Branch), request.BranchId);
        }

        if (!branch.IsActive)
        {
            throw new InvalidOperationException($"Cannot assign staff to inactive branch '{branch.Name}'.");
        }

        var emailNorm = request.Email.Trim().ToLowerInvariant();
        var user = await _context.UserAccounts.FirstOrDefaultAsync(
            u => u.Email.ToLower() == emailNorm,
            cancellationToken);

        if (user != null)
        {
            var existingRole = StaffRoleNormalizer.Normalize(user.Role);
            if (existingRole == "admin")
            {
                throw new InvalidOperationException("Cannot modify administrator accounts.");
            }

            if (StaffRoleNormalizer.IsBranchManager(request.CurrentUserRole) && existingRole == "branch_manager")
            {
                throw new InvalidOperationException("Branch managers cannot modify other branch manager accounts.");
            }
        }

        string? tempPassword = null;

        if (user == null)
        {
            tempPassword = string.IsNullOrWhiteSpace(request.Password)
                ? GenerateTemporaryPassword()
                : request.Password!;

            user = new UserAccount
            {
                Id = Guid.NewGuid(),
                Email = request.Email.Trim(),
                DisplayName = request.FullName.Trim(),
                Phone = request.Phone,
                Role = roleNorm,
                IsActive = true,
                PasswordHash = _passwordService.HashPassword(tempPassword),
                CreatedAt = DateTime.UtcNow,
                EmailVerified = false,
            };
            _context.UserAccounts.Add(user);
        }
        else
        {
            user.DisplayName = request.FullName.Trim();
            user.Phone = request.Phone;
            user.Role = roleNorm;
            user.IsActive = true;
            user.UpdatedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                user.PasswordHash = _passwordService.HashPassword(request.Password!);
            }
            else if (string.IsNullOrEmpty(user.PasswordHash))
            {
                tempPassword = GenerateTemporaryPassword();
                user.PasswordHash = _passwordService.HashPassword(tempPassword);
            }
        }

        var existingAssignments = await _context.StaffAssignments
            .Where(sa => sa.StaffId == user.Id)
            .ToListAsync(cancellationToken);
        _context.StaffAssignments.RemoveRange(existingAssignments);

        var assignment = new StaffAssignment
        {
            Id = Guid.NewGuid(),
            StaffId = user.Id,
            BranchId = request.BranchId,
            Position = roleNorm,
            IsPrimary = true,
            Permissions = JsonSerializer.SerializeToDocument(new
            {
                can_manage_inventory = true,
                can_manage_orders = true,
                can_manage_staff = roleNorm == "branch_manager",
                can_view_other_branches = false,
            }),
            AssignedAt = DateTime.UtcNow,
        };
        _context.StaffAssignments.Add(assignment);

        await _context.SaveChangesAsync(cancellationToken);

        try
        {
            await StaffAssignmentEmailNotifier.SendStaffAssignedAsync(
                _emailService,
                user.Email,
                user.DisplayName,
                branch.Name,
                roleNorm,
                tempPassword,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send staff assignment email to {Email}", user.Email);
        }

        return ToDto(user, request.BranchId, branch.Name);
    }

    private static BranchStaffAccountUserDto ToDto(UserAccount u, Guid branchId, string branchName)
    {
        SplitDisplayName(u.DisplayName, out var firstName, out var lastName);
        var role = StaffRoleNormalizer.Normalize(u.Role);

        return new BranchStaffAccountUserDto
        {
            Id = u.Id,
            FirstName = firstName,
            LastName = lastName,
            FullName = u.DisplayName ?? "Anonymous",
            Email = u.Email,
            Role = role,
            Status = u.IsActive ? "Active" : "Suspended",
            Phone = u.Phone ?? "",
            BranchId = branchId,
            BranchName = branchName,
        };
    }

    private static void SplitDisplayName(string? displayName, out string firstName, out string lastName)
    {
        firstName = "";
        lastName = "";
        if (string.IsNullOrWhiteSpace(displayName))
            return;

        var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return;
        if (parts.Length == 1)
        {
            firstName = parts[0];
            return;
        }

        lastName = parts[^1];
        firstName = string.Join(' ', parts.Take(parts.Length - 1));
    }

    private static string GenerateTemporaryPassword()
    {
        var bytes = RandomNumberGenerator.GetBytes(12);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', 'A').Replace('/', 'Z')[..12];
    }
}
