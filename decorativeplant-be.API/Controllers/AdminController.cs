using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.API.Controllers;

/// <summary>
/// Admin-only endpoints for user management, seller oversight, reports, and dashboard stats.
/// Matches FE admin-service.ts API calls.
/// </summary>
[ApiController]
[Route("api/admin")]
// [Authorize(Roles = "Admin,admin,super_admin")] // Enable when JWT roles are configured
public class AdminController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly IPasswordService _passwordService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IApplicationDbContext context,
        IEmailService emailService,
        IPasswordService passwordService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _emailService = emailService;
        _passwordService = passwordService;
        _logger = logger;
    }

    // ─── Users ────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers(CancellationToken cancellationToken)
    {
        var users = await _context.UserAccounts
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync(cancellationToken);

        var primaries = await _context.StaffAssignments
            .AsNoTracking()
            .Where(sa => sa.IsPrimary)
            .Join(
                _context.Branches.AsNoTracking(),
                sa => sa.BranchId,
                b => b.Id,
                (sa, b) => new { sa.StaffId, sa.BranchId, BranchName = b.Name })
            .ToListAsync(cancellationToken);

        var branchByStaff = primaries.ToDictionary(x => x.StaffId);

        var result = users.Select(u =>
        {
            branchByStaff.TryGetValue(u.Id, out var br);
            return ToUserDto(u, br?.BranchId, br?.BranchName);
        }).ToList();

        return Ok(result);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id, CancellationToken cancellationToken)
    {
        var u = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (u == null)
            return NotFound(ApiResponse<object>.ErrorResponse("User not found", statusCode: 404));

        var primary = await _context.StaffAssignments
            .AsNoTracking()
            .Where(sa => sa.StaffId == id && sa.IsPrimary)
            .Join(
                _context.Branches.AsNoTracking(),
                sa => sa.BranchId,
                b => b.Id,
                (sa, b) => new { sa.BranchId, BranchName = b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(ToUserDetailDto(u, primary?.BranchId, primary?.BranchName));
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateAdminUserRequest request, CancellationToken cancellationToken)
    {
        var emailNorm = request.Email.Trim().ToLowerInvariant();
        var exists = await _context.UserAccounts.AnyAsync(
            x => x.Email.ToLower() == emailNorm,
            cancellationToken);
        if (exists)
            return Conflict(ApiResponse<object>.ErrorResponse("Email already registered.", statusCode: 409));

        var role = StaffRoleNormalizer.Normalize(request.Role);
        var user = new UserAccount
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            DisplayName = $"{request.FirstName} {request.LastName}".Trim(),
            Phone = request.Phone,
            Bio = request.Biography,
            Role = role,
            IsActive = request.Status == "Active",
            PasswordHash = _passwordService.HashPassword(request.Password),
            CreatedAt = DateTime.UtcNow,
            EmailVerified = false,
        };

        if (!string.IsNullOrEmpty(request.Avatar) && request.Avatar.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            user.AvatarUrl = request.Avatar;

        _context.UserAccounts.Add(user);
        await _context.SaveChangesAsync(cancellationToken);

        return Ok(ToUserDto(user, null, null));
    }

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (user == null)
            return NotFound(ApiResponse<object>.ErrorResponse("User not found", statusCode: 404));

        if (request.FullName != null)
            user.DisplayName = request.FullName;
        else if (request.FirstName != null || request.LastName != null)
        {
            SplitDisplayName(user.DisplayName, out var existingFirst, out var existingLast);
            var fn = request.FirstName ?? existingFirst;
            var ln = request.LastName ?? existingLast;
            user.DisplayName = $"{fn} {ln}".Trim();
        }

        if (request.Email != null)
        {
            var emailNorm = request.Email.Trim().ToLowerInvariant();
            var taken = await _context.UserAccounts.AnyAsync(
                x => x.Id != id && x.Email.ToLower() == emailNorm,
                cancellationToken);
            if (taken)
                return Conflict(ApiResponse<object>.ErrorResponse("Email is already in use.", statusCode: 409));
            user.Email = request.Email.Trim();
        }

        if (request.Role != null)
            user.Role = StaffRoleNormalizer.Normalize(request.Role);

        if (request.Phone != null)
            user.Phone = request.Phone;

        if (request.Biography != null)
            user.Bio = request.Biography;

        if (request.Status != null)
            user.IsActive = request.Status == "Active";

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        var primary = await _context.StaffAssignments
            .AsNoTracking()
            .Where(sa => sa.StaffId == id && sa.IsPrimary)
            .Join(
                _context.Branches.AsNoTracking(),
                sa => sa.BranchId,
                b => b.Id,
                (sa, b) => new { sa.BranchId, BranchName = b.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return Ok(ToUserDto(user, primary?.BranchId, primary?.BranchName));
    }

    [HttpDelete("users/{id:guid}")]
    public async Task<IActionResult> DeleteUser(Guid id, CancellationToken cancellationToken)
    {
        var user = await _context.UserAccounts
            .Include(u => u.StaffAssignments)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (user == null)
            return NotFound(ApiResponse<object>.ErrorResponse("User not found", statusCode: 404));

        if (StaffRoleNormalizer.Normalize(user.Role) == "admin")
            return BadRequest(ApiResponse<object>.ErrorResponse("Cannot remove administrator accounts.", statusCode: 400));

        _context.StaffAssignments.RemoveRange(user.StaffAssignments);
        user.Role = "customer";
        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    // ─── Staff (user_account + staff_assignment + email) ───────────────────

    [HttpPost("staff")]
    public Task<IActionResult> CreateStaff([FromBody] AdminStaffUpsertRequest request, CancellationToken cancellationToken) =>
        UpsertStaffCoreAsync(userId: null, request, cancellationToken);

    [HttpPut("staff/{userId:guid}")]
    public Task<IActionResult> UpdateStaff(Guid userId, [FromBody] AdminStaffUpsertRequest request, CancellationToken cancellationToken) =>
        UpsertStaffCoreAsync(userId, request, cancellationToken);

    private async Task<IActionResult> UpsertStaffCoreAsync(Guid? userId, AdminStaffUpsertRequest request, CancellationToken cancellationToken)
    {
        var roleNorm = StaffRoleNormalizer.Normalize(request.Role);
        if (!StaffRoleNormalizer.BranchAssignableRoles.Contains(roleNorm))
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "Role must be branch_manager, store_staff, cultivation_staff, or fulfillment_staff.",
                statusCode: 400));
        }

        if (request.BranchId == Guid.Empty)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse(
                "branchId is required. Choose an active branch from the list.",
                statusCode: 400));
        }

        var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == request.BranchId, cancellationToken);
        if (branch == null)
            return NotFound(ApiResponse<object>.ErrorResponse("Branch not found.", statusCode: 404));

        if (!branch.IsActive)
            return BadRequest(ApiResponse<object>.ErrorResponse("Cannot assign staff to an inactive branch.", statusCode: 400));

        var emailNorm = request.Email.Trim().ToLowerInvariant();
        UserAccount? user = null;

        if (userId.HasValue)
        {
            user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);
            if (user == null)
                return NotFound(ApiResponse<object>.ErrorResponse("User not found.", statusCode: 404));

            if (user.Email.ToLower() != emailNorm)
            {
                var emailTaken = await _context.UserAccounts.AnyAsync(
                    u => u.Id != user.Id && u.Email.ToLower() == emailNorm,
                    cancellationToken);
                if (emailTaken)
                    return Conflict(ApiResponse<object>.ErrorResponse("Email is already in use.", statusCode: 409));
                user.Email = request.Email.Trim();
            }
        }
        else
        {
            user = await _context.UserAccounts.FirstOrDefaultAsync(
                u => u.Email.ToLower() == emailNorm,
                cancellationToken);
        }

        string? tempPassword = null;
        var isNewAccount = false;

        if (user == null)
        {
            isNewAccount = true;
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
                user.PasswordHash = _passwordService.HashPassword(request.Password!);
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

        return Ok(ToUserDto(user, request.BranchId, branch.Name));
    }

    // ─── Sellers (branches mapped as sellers) ────────────────────────────

    [HttpGet("sellers")]
    public async Task<IActionResult> GetSellers()
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .Select(b => new
            {
                id = b.Id,
                sellerId = "#BR-" + b.Id.ToString().Substring(0, 5).ToUpper(),
                name = b.Name,
                email = "branch" + b.Id.ToString().Substring(0, 4) + "@sopss.com",
                joinDate = b.CreatedAt.ToString("MMM dd, yyyy"),
                products = 10, // Placeholder
                status = b.IsActive ? "Active" : "Inactive",
                avatar = "https://ui-avatars.com/api/?name=" + b.Name
            })
            .ToListAsync();

        return Ok(branches);
    }

    [HttpGet("sellers/{id:guid}")]
    public async Task<IActionResult> GetSellerById(Guid id)
    {
        var b = await _context.Branches.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (b == null)
            return NotFound(ApiResponse<object>.ErrorResponse("Seller not found", statusCode: 404));

        return Ok(new
        {
            id = b.Id,
            sellerId = "#BR-" + b.Id.ToString().Substring(0, 5).ToUpper(),
            name = b.Name,
            email = "branch" + b.Id.ToString().Substring(0, 4) + "@sopss.com",
            joinDate = b.CreatedAt.ToString("MMM dd, yyyy"),
            products = 10,
            status = b.IsActive ? "Active" : "Inactive",
            avatar = "https://ui-avatars.com/api/?name=" + b.Name,
            owner = b.Name,
            phone = "",
            address = "",
            storeImage = "https://ui-avatars.com/api/?name=" + b.Name,
            businessDescription = "",
            taxId = "",
            businessType = b.BranchType ?? "Retail",
            verificationChecklist = new[]
            {
                new { label = "Business License", verified = true, needsReview = false },
                new { label = "Tax Registration", verified = false, needsReview = true }
            },
            documents = Array.Empty<object>(),
            performanceHistory = Array.Empty<object>()
        });
    }

    [HttpPatch("sellers/{id:guid}/status")]
    public async Task<IActionResult> UpdateSellerStatus(Guid id, [FromBody] UpdateSellerStatusRequest request)
    {
        var branch = await _context.Branches.FirstOrDefaultAsync(x => x.Id == id);
        if (branch == null)
            return NotFound(ApiResponse<object>.ErrorResponse("Seller not found", statusCode: 404));

        branch.IsActive = request.Status switch
        {
            "Active" => true,
            "Suspended" or "Rejected" or "Inactive" => false,
            _ => branch.IsActive
        };
        branch.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = branch.Id,
            sellerId = "#BR-" + branch.Id.ToString().Substring(0, 5).ToUpper(),
            name = branch.Name,
            email = "branch" + branch.Id.ToString().Substring(0, 4) + "@sopss.com",
            joinDate = branch.CreatedAt.ToString("MMM dd, yyyy"),
            products = 10,
            status = branch.IsActive ? "Active" : "Inactive",
            avatar = "https://ui-avatars.com/api/?name=" + branch.Name
        });
    }

    // ─── Reports (mock — no Report entity in Domain yet) ─────────────────

    [HttpGet("reports")]
    public Task<IActionResult> GetReports()
    {
        var reports = new[]
        {
            new
            {
                id = "1",
                reportId = "REP-2024-001",
                reporter = new
                {
                    name = "Sarah Jenkins",
                    email = "sarah.j@example.com",
                    avatar = "https://i.pravatar.cc/150?img=5",
                    initials = "SJ",
                    initialsColor = "#4f46e5",
                    joinDate = "2023-06-01",
                    reportsFiled = 3,
                    trustScore = 85
                },
                reason = "Inappropriate Product Listing",
                date = "Oct 24, 2023",
                status = "Pending",
                priority = "High"
            },
            new
            {
                id = "2",
                reportId = "REP-2024-002",
                reporter = new
                {
                    name = "Michael Chen",
                    email = "m.chen@example.com",
                    avatar = "https://i.pravatar.cc/150?img=8",
                    initials = "MC",
                    initialsColor = "#059669",
                    joinDate = "2023-03-15",
                    reportsFiled = 1,
                    trustScore = 92
                },
                reason = "Misleading Description",
                date = "Oct 25, 2023",
                status = "Under Review",
                priority = "Medium"
            }
        };
        IActionResult result = Ok(reports);
        return Task.FromResult(result);
    }

    [HttpGet("reports/{id}")]
    public Task<IActionResult> GetReportById(string id)
    {
        if (id == "1")
        {
            var detail = new
            {
                id = "1",
                reportId = "REP-2024-001",
                reporter = new
                {
                    name = "Sarah Jenkins",
                    email = "sarah.j@example.com",
                    avatar = "https://i.pravatar.cc/150?img=5",
                    initials = "SJ",
                    initialsColor = "#4f46e5",
                    joinDate = "2023-06-01",
                    reportsFiled = 3,
                    trustScore = 85
                },
                reason = "Inappropriate Product Listing",
                date = "Oct 24, 2023",
                status = "Pending",
                priority = "High",
                sellerName = "GreenLeaf Store",
                category = "Product Quality",
                description = "The product listing contains misleading information about plant species.",
                evidence = new[] { "screenshot1.png", "screenshot2.png" },
                adminNotes = Array.Empty<object>(),
                timeline = Array.Empty<object>()
            };
            IActionResult result = Ok(detail);
            return Task.FromResult(result);
        }

        IActionResult notFound = NotFound(ApiResponse<object>.ErrorResponse("Report not found", statusCode: 404));
        return Task.FromResult(notFound);
    }

    [HttpPatch("reports/{id}/status")]
    public Task<IActionResult> UpdateReportStatus(string id, [FromBody] UpdateReportStatusRequest request)
    {
        var result = new
        {
            id,
            status = request.Status,
            note = request.Note
        };
        IActionResult ok = Ok(result);
        return Task.FromResult(ok);
    }

    // ─── Stats ─────────────────────────────────────────────────────────────

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalUsers = await _context.UserAccounts.CountAsync();
        var activeUsers = await _context.UserAccounts.CountAsync(u => u.IsActive);
        var totalSellers = await _context.Branches.CountAsync();
        var activeSellers = await _context.Branches.CountAsync(b => b.IsActive);

        return Ok(new
        {
            totalSellers,
            activeSellers,
            pendingSellers = totalSellers - activeSellers,
            suspendedSellers = 0,
            totalReports = 2,
            pendingReports = 1,
            resolvedReports = 1,
            rejectedReports = 0,
            totalUsers,
            activeUsers
        });
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

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

    private static object ToUserDto(UserAccount u, Guid? branchId, string? branchName)
    {
        SplitDisplayName(u.DisplayName, out var firstName, out var lastName);
        var role = StaffRoleNormalizer.Normalize(u.Role);
        var nameForAvatar = Uri.EscapeDataString(u.DisplayName ?? u.Email);

        return new
        {
            id = u.Id,
            firstName,
            lastName,
            fullName = u.DisplayName ?? "Anonymous",
            email = u.Email,
            role,
            status = u.IsActive ? "Active" : "Suspended",
            phone = u.Phone ?? "",
            biography = u.Bio ?? "",
            avatar = u.AvatarUrl ?? $"https://ui-avatars.com/api/?name={nameForAvatar}",
            joinDate = u.CreatedAt.ToString("MMM dd, yyyy"),
            twoFactorEnabled = false,
            lastLogin = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToString("g") : "Never",
            branchId,
            branchName,
        };
    }

    private static object ToUserDetailDto(UserAccount u, Guid? branchId, string? branchName)
    {
        SplitDisplayName(u.DisplayName, out var firstName, out var lastName);
        var role = StaffRoleNormalizer.Normalize(u.Role);
        var nameForAvatar = Uri.EscapeDataString(u.DisplayName ?? u.Email);

        return new
        {
            id = u.Id,
            firstName,
            lastName,
            fullName = u.DisplayName ?? "Anonymous",
            email = u.Email,
            role,
            status = u.IsActive ? "Active" : "Suspended",
            phone = u.Phone ?? "",
            biography = u.Bio ?? "",
            avatar = u.AvatarUrl ?? $"https://ui-avatars.com/api/?name={nameForAvatar}",
            joinDate = u.CreatedAt.ToString("MMM dd, yyyy"),
            twoFactorEnabled = false,
            lastLogin = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToString("g") : "Never",
            branchId,
            branchName,
            isProfileCompleted = u.IsProfileCompleted,
            experienceLevel = u.ExperienceLevel,
            sunlightExposure = u.SunlightExposure,
            roomTemperatureRange = u.RoomTemperatureRange,
            humidityLevel = u.HumidityLevel,
            wateringFrequency = u.WateringFrequency,
            plantPlacement = u.PlacementLocation,
            spaceSize = u.SpaceSize,
            hasChildren = u.HasChildrenOrPets,
            hasPets = u.HasChildrenOrPets,
            plantGoals = u.PlantGoals != null ? u.PlantGoals.RootElement : (object?)null,
            stylePreference = u.PreferredStyle,
            budget = u.BudgetRange,
            location = u.LocationCity
        };
    }
}

// ─── Request DTOs ───────────────────────────────────────────────────────

public class UpdateUserRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Biography { get; set; }
    public string? Status { get; set; }
}

public class CreateAdminUserRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "customer";
    public string Status { get; set; } = "Active";
    public string? Biography { get; set; }
    public string? Avatar { get; set; }
}

public class AdminStaffUpsertRequest
{
    public string Email { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string Role { get; set; } = "";
    public Guid BranchId { get; set; }
    /// <summary>Optional. For new accounts, a random password is generated when omitted.</summary>
    public string? Password { get; set; }
}

public class UpdateSellerStatusRequest
{
    public string Status { get; set; } = string.Empty;
}

public class UpdateReportStatusRequest
{
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}
