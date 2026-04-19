using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Application.Services;

/// <summary>
/// Workload-based staff assignment: picks the fulfillment_staff at the order's
/// branch who has the fewest active orders (confirmed/processing/shipping).
/// If all staff are at or above <see cref="MaxActiveOrders"/> the order is left
/// unassigned (AssignedStaffId = null) for the queue job to retry later.
/// </summary>
public class OrderAssignmentService : IOrderAssignmentService
{
    /// Maximum active orders per staff before they are considered at capacity.
    public const int MaxActiveOrders = 10;

    private static readonly string[] ActiveStatuses =
    {
        "confirmed", "processing", "shipping", "shipped", "in_transit",
    };

    private readonly IApplicationDbContext _context;
    private readonly ILogger<OrderAssignmentService> _logger;

    public OrderAssignmentService(
        IApplicationDbContext context,
        ILogger<OrderAssignmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserAccount?> TryAssignAsync(OrderHeader order, CancellationToken ct = default)
    {
        var branchId = order.OrderItems?.FirstOrDefault()?.BranchId;
        if (branchId == null)
        {
            _logger.LogWarning("OrderAssignmentService: Order {OrderCode} has no branch, cannot assign.", order.OrderCode);
            return null;
        }

        // Get all active fulfillment_staff at this branch
        var staffIds = await (
            from sa in _context.StaffAssignments
            join u in _context.UserAccounts on sa.StaffId equals u.Id
            where sa.BranchId == branchId.Value
               && u.Role == "fulfillment_staff"
               && u.IsActive
            orderby sa.AssignedAt  // tie-break: earlier assignment = higher priority
            select new { u.Id, u.DisplayName }
        ).ToListAsync(ct);

        if (staffIds.Count == 0)
        {
            _logger.LogInformation("OrderAssignmentService: No fulfillment_staff at branch {BranchId}.", branchId);
            return null;
        }

        // Count active orders per staff in one query
        var staffIdList = staffIds.Select(s => s.Id).ToList();
        var workloads = await _context.OrderHeaders
            .Where(o => o.AssignedStaffId != null
                     && staffIdList.Contains(o.AssignedStaffId!.Value)
                     && o.Status != null
                     && ActiveStatuses.Contains(o.Status))
            .GroupBy(o => o.AssignedStaffId!.Value)
            .Select(g => new { StaffId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StaffId, x => x.Count, ct);

        // Pick staff with lowest workload under the cap (preserve order for tie-break)
        var candidate = staffIds
            .Select(s => new { s.Id, s.DisplayName, Load = workloads.GetValueOrDefault(s.Id, 0) })
            .Where(s => s.Load < MaxActiveOrders)
            .OrderBy(s => s.Load)
            .FirstOrDefault();

        if (candidate == null)
        {
            _logger.LogInformation(
                "OrderAssignmentService: All {Count} staff at branch {BranchId} are at capacity ({Max}). Order {OrderCode} queued.",
                staffIds.Count, branchId, MaxActiveOrders, order.OrderCode);
            return null;
        }

        // Persist assignment
        order.AssignedStaffId = candidate.Id;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "OrderAssignmentService: Order {OrderCode} assigned to staff {StaffId} ({Name}) with {Load} active orders.",
            order.OrderCode, candidate.Id, candidate.DisplayName, candidate.Load);

        return await _context.UserAccounts.FindAsync([candidate.Id], ct);
    }

    public async Task TryFlushQueueAsync(Guid branchId, CancellationToken ct = default)
    {
        // Find the oldest queued confirmed order at this branch
        var queued = await _context.OrderHeaders
            .Include(o => o.OrderItems)
            .Where(o => o.AssignedStaffId == null
                     && o.Status != null
                     && ActiveStatuses.Contains(o.Status)
                     && o.OrderItems!.Any(i => i.BranchId == branchId))
            .OrderBy(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (queued == null) return;

        var assigned = await TryAssignAsync(queued, ct);
        if (assigned != null)
            _logger.LogInformation(
                "OrderAssignmentService: Slot freed at branch {BranchId} — queued Order {OrderCode} assigned to {StaffId}.",
                branchId, queued.OrderCode, assigned.Id);
    }
}
