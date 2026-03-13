using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/admin")]
// [Authorize(Roles = "Admin")] // Uncomment this when you have JWT working
public class AdminController : ControllerBase
{
    private readonly IApplicationDbContext _context;

    public AdminController(IApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _context.UserAccounts
            .AsNoTracking()
            .OrderByDescending(u => u.CreatedAt)
            .Select(u => new {
                id = u.Id,
                fullName = u.DisplayName ?? "Anonymous",
                email = u.Email,
                role = u.Role,
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

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var totalUsers = await _context.UserAccounts.CountAsync();
        var activeUsers = await _context.UserAccounts.CountAsync(u => u.IsActive);
        
        // Mocking some stats for the dashboard to look full
        return Ok(new {
            totalSellers = 12, // Placeholder
            activeSellers = 10,
            pendingSellers = 2,
            suspendedSellers = 0,
            totalReports = 5,
            pendingReports = 2,
            resolvedReports = 3,
            rejectedReports = 0,
            totalUsers = totalUsers,
            activeUsers = activeUsers
        });
    }

    [HttpGet("sellers")]
    public async Task<IActionResult> GetSellers()
    {
        // For this project, a "Seller" might be a Branch or a User with a specific role
        // Mapping Branches as Sellers for now since they represent stores
        var branches = await _context.Branches
            .AsNoTracking()
            .Select(b => new {
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

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports()
    {
        // Mocking reports for FE test
        var reports = new[] {
            new {
                id = "1",
                reportId = "REP-2024-001",
                reporter = new {
                    name = "Sarah Jenkins",
                    email = "sarah.j@example.com",
                    avatar = "https://i.pravatar.cc/150?img=5"
                },
                reason = "Inappropriate Product Listing",
                date = "Oct 24, 2023",
                status = "Pending",
                priority = "High"
            },
            new {
                id = "2",
                reportId = "REP-2024-002",
                reporter = new {
                    name = "Michael Chen",
                    email = "m.chen@example.com",
                    avatar = "https://i.pravatar.cc/150?img=8"
                },
                reason = "Misleading Description",
                date = "Oct 25, 2023",
                status = "Under Review",
                priority = "Medium"
            }
        };
        return Ok(reports);
    }
}
