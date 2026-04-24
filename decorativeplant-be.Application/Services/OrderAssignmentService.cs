using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Application.Services;

/// <summary>
/// Workload-based fulfillment_staff assignment at the order's branch.
/// Online web home-delivery and offline counter delivery use the same workload algorithm in
/// <see cref="TryApplyWorkloadFulfillmentAssignmentAsync"/> but separate entry methods so
/// offline counter rules can evolve without changing the online-only path.
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

    /// <inheritdoc />
    public async Task<UserAccount?> TryAssignAsync(OrderHeader order, CancellationToken ct = default)
    {
        if (order.AssignedStaffId.HasValue)
            return await _context.UserAccounts.FindAsync([order.AssignedStaffId.Value], ct);

        if (OrderTypeInfoHelper.IsOfflineCounterDeliveryShipOrder(order))
            return await TryAssignOfflineCounterDeliveryInternalAsync(order, ct);

        return await TryAssignOnlineWebDeliveryInternalAsync(order, ct);
    }

    /// <summary>Online web checkout home-delivery only — not called for offline counter ship orders.</summary>
    private async Task<UserAccount?> TryAssignOnlineWebDeliveryInternalAsync(OrderHeader order, CancellationToken ct)
    {
        if (OrderTypeInfoHelper.IsPickupAtBranchOrder(order))
            return null;

        // Staff offline flows (non-delivery, or any offline if misrouted) do not use this path.
        if (OrderTypeInfoHelper.IsOfflineChannelOrder(order))
            return null;

        return await TryApplyWorkloadFulfillmentAssignmentAsync(order, "online_web_delivery", ct);
    }

    /// <summary>Counter-created ship-to-customer offline orders — same workload rules, separate entry.</summary>
    private async Task<UserAccount?> TryAssignOfflineCounterDeliveryInternalAsync(OrderHeader order, CancellationToken ct)
    {
        if (!OrderTypeInfoHelper.IsOfflineCounterDeliveryShipOrder(order))
        {
            _logger.LogWarning(
                "OrderAssignmentService: TryAssignOfflineCounterDeliveryInternalAsync called for non-offline-delivery Order {OrderCode}.",
                order.OrderCode);
            return null;
        }

        return await TryApplyWorkloadFulfillmentAssignmentAsync(order, "offline_counter_delivery", ct);
    }

    /// <summary>Shared workload algorithm (lowest active load under cap).</summary>
    private async Task<UserAccount?> TryApplyWorkloadFulfillmentAssignmentAsync(
        OrderHeader order,
        string assignmentSource,
        CancellationToken ct)
    {
        var branchId = order.OrderItems?.FirstOrDefault()?.BranchId;
        if (branchId == null)
        {
            _logger.LogWarning("OrderAssignmentService: Order {OrderCode} has no branch, cannot assign.", order.OrderCode);
            return null;
        }

        var staffIds = await (
            from sa in _context.StaffAssignments
            join u in _context.UserAccounts on sa.StaffId equals u.Id
            where sa.BranchId == branchId.Value
               && u.Role == "fulfillment_staff"
               && u.IsActive
            orderby sa.AssignedAt
            select new { u.Id, u.DisplayName }
        ).ToListAsync(ct);

        if (staffIds.Count == 0)
        {
            _logger.LogInformation("OrderAssignmentService: No fulfillment_staff at branch {BranchId}.", branchId);
            return null;
        }

        var staffIdList = staffIds.Select(s => s.Id).ToList();
        var workloads = await _context.OrderHeaders
            .Where(o => o.AssignedStaffId != null
                     && staffIdList.Contains(o.AssignedStaffId!.Value)
                     && o.Status != null
                     && ActiveStatuses.Contains(o.Status))
            .GroupBy(o => o.AssignedStaffId!.Value)
            .Select(g => new { StaffId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StaffId, x => x.Count, ct);

        var candidate = staffIds
            .Select(s => new { s.Id, s.DisplayName, Load = workloads.GetValueOrDefault(s.Id, 0) })
            .Where(s => s.Load < MaxActiveOrders)
            .OrderBy(s => s.Load)
            .FirstOrDefault();

        if (candidate == null)
        {
            _logger.LogInformation(
                "OrderAssignmentService: All {Count} staff at branch {BranchId} are at capacity ({Max}). Order {OrderCode} queued ({Source}).",
                staffIds.Count, branchId, MaxActiveOrders, order.OrderCode, assignmentSource);
            return null;
        }

        order.AssignedStaffId = candidate.Id;
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation(
            "OrderAssignmentService: Order {OrderCode} assigned to staff {StaffId} ({Name}) with {Load} active orders ({Source}).",
            order.OrderCode, candidate.Id, candidate.DisplayName, candidate.Load, assignmentSource);

        return await _context.UserAccounts.FindAsync([candidate.Id], ct);
    }

    public async Task TryFlushQueueAsync(Guid branchId, CancellationToken ct = default)
    {
        var candidates = await _context.OrderHeaders
            .Include(o => o.OrderItems)
            .Where(o => o.AssignedStaffId == null
                     && o.Status != null
                     && ActiveStatuses.Contains(o.Status)
                     && o.OrderItems!.Any(i => i.BranchId == branchId))
            .OrderBy(o => o.CreatedAt)
            .Take(25)
            .ToListAsync(ct);

        var queued = candidates.FirstOrDefault(o => !OrderTypeInfoHelper.SkipsFulfillmentWorkloadPipeline(o));
        if (queued == null) return;

        var assigned = await TryAssignAsync(queued, ct);
        if (assigned != null)
            _logger.LogInformation(
                "OrderAssignmentService: Slot freed at branch {BranchId} — queued Order {OrderCode} assigned to {StaffId}.",
                branchId, queued.OrderCode, assigned.Id);
    }
}
