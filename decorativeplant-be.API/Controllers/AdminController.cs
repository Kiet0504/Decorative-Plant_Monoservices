using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;

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

    public AdminController(IApplicationDbContext context)
    {
        _context = context;
    }

    // ─── Users ────────────────────────────────────────────────────────────

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.UserAccounts
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new
            {
                id = u.Id,
                fullName = u.DisplayName ?? "Anonymous",
                email = u.Email,
                role = MapRole(u.Role),
                status = u.IsActive ? "Active" : "Suspended",
                phone = u.Phone ?? "",
                biography = u.Bio ?? "",
                avatar = u.AvatarUrl ?? "https://ui-avatars.com/api/?name=" + (u.DisplayName ?? u.Email),
                joinDate = u.CreatedAt.ToString("MMM dd, yyyy"),
                twoFactorEnabled = false,
                lastLogin = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToString("g") : "Never"
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var u = await _context.UserAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (u == null)
            return NotFound(ApiResponse<object>.ErrorResponse("User not found", statusCode: 404));

        return Ok(new
        {
            // ===== EXISTING FIELDS =====
            id = u.Id,
            fullName = u.DisplayName ?? "Anonymous",
            email = u.Email,
            role = MapRole(u.Role),
            status = u.IsActive ? "Active" : "Suspended",
            phone = u.Phone ?? "",
            biography = u.Bio ?? "",
            avatar = u.AvatarUrl ?? "https://ui-avatars.com/api/?name=" + (u.DisplayName ?? u.Email),
            joinDate = u.CreatedAt.ToString("MMM dd, yyyy"),
            twoFactorEnabled = false,
            lastLogin = u.LastLoginAt.HasValue ? u.LastLoginAt.Value.ToString("g") : "Never",

            // ===== ONBOARDING PROFILE FIELDS =====
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
        });
    }

    [HttpPut("users/{id:guid}")]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UpdateUserRequest request)
    {
        var user = await _context.UserAccounts.FirstOrDefaultAsync(x => x.Id == id);
        if (user == null)
            return NotFound(ApiResponse<object>.ErrorResponse("User not found", statusCode: 404));

        if (request.FullName != null) user.DisplayName = request.FullName;
        if (request.Role != null) user.Role = ReverseMapRole(request.Role);
        if (request.Phone != null) user.Phone = request.Phone;
        if (request.Biography != null) user.Bio = request.Biography;
        if (request.Status != null)
        {
            user.IsActive = request.Status == "Active";
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(new
        {
            id = user.Id,
            fullName = user.DisplayName ?? "Anonymous",
            email = user.Email,
            role = MapRole(user.Role),
            status = user.IsActive ? "Active" : "Suspended",
            phone = user.Phone ?? "",
            biography = user.Bio ?? "",
            avatar = user.AvatarUrl ?? "",
            joinDate = user.CreatedAt.ToString("MMM dd, yyyy"),
            twoFactorEnabled = false,
            lastLogin = user.LastLoginAt.HasValue ? user.LastLoginAt.Value.ToString("g") : "Never"
        });
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
            // SellerDetail extra fields
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
        // Mock detail for the first report
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
        // Mock response — Report entity not yet in Domain
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
            totalReports = 2,   // Mock until Report entity exists
            pendingReports = 1,
            resolvedReports = 1,
            rejectedReports = 0,
            totalUsers,
            activeUsers
        });
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static string MapRole(string role) => role switch
    {
        "super_admin" or "admin" => "Admin",
        "branch_manager" => "Moderator",
        "staff" or "cultivationStaff" or "storeStaff" or "fulfillmentStaff" => "Editor",
        _ => "User"
    };

    private static string ReverseMapRole(string feRole) => feRole switch
    {
        "Admin" => "admin",
        "Moderator" => "branch_manager",
        "Editor" => "staff",
        _ => "customer"
    };
}

// ─── Request DTOs ───────────────────────────────────────────────────────

public class UpdateUserRequest
{
    public string? FullName { get; set; }
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Biography { get; set; }
    public string? Status { get; set; }
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
