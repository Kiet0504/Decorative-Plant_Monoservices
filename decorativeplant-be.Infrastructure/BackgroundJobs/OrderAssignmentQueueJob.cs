using System.Net;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.DTOs.Email;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.BackgroundJobs;

/// <summary>
/// Retries assignment for queued orders every 2 minutes.
/// Also alerts branch managers when an order has been unassigned for longer
/// than <see cref="StuckThreshold"/> (15 min), detected once via "just-crossed" window.
/// </summary>
public class OrderAssignmentQueueJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OrderAssignmentQueueJob> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(2);

    // Alert when an order has been waiting longer than this
    private static readonly TimeSpan StuckThreshold = TimeSpan.FromMinutes(15);

    // Statuses where an unassigned order still needs fulfillment
    private static readonly string[] QueueableStatuses = { "confirmed", "processing" };

    public OrderAssignmentQueueJob(
        IServiceScopeFactory scopeFactory,
        ILogger<OrderAssignmentQueueJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderAssignmentQueueJob started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueuedOrdersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OrderAssignmentQueueJob: Unhandled error during queue processing.");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task ProcessQueuedOrdersAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var assignmentService = scope.ServiceProvider.GetRequiredService<IOrderAssignmentService>();
        var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

        var queuedOrders = await context.OrderHeaders
            .Include(o => o.OrderItems)
            .Where(o => o.AssignedStaffId == null
                     && o.Status != null
                     && QueueableStatuses.Contains(o.Status))
            .OrderBy(o => o.CreatedAt)  // FIFO
            .Take(50)
            .ToListAsync(ct);

        if (queuedOrders.Count == 0) return;

        _logger.LogInformation("OrderAssignmentQueueJob: Processing {Count} queued orders.", queuedOrders.Count);

        int assigned = 0;
        var stillQueued = new List<decorativeplant_be.Domain.Entities.OrderHeader>();

        foreach (var order in queuedOrders)
        {
            try
            {
                var staff = await assignmentService.TryAssignAsync(order, ct);
                if (staff == null)
                {
                    stillQueued.Add(order);
                    // All staff at this branch are at capacity — no point trying remaining orders for same branch
                    // but continue for orders that might belong to other branches
                    continue;
                }

                await NewOrderForStaffNotifier.NotifyAsync(
                    order, context, emailService, _logger, assignmentService, ct);

                assigned++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OrderAssignmentQueueJob: Failed to process queued Order {OrderCode}.", order.OrderCode);
                stillQueued.Add(order);
            }
        }

        if (assigned > 0)
            _logger.LogInformation("OrderAssignmentQueueJob: Assigned {Assigned}/{Total} queued orders.", assigned, queuedOrders.Count);

        // Alert branch managers for orders that just crossed the stuck threshold
        await AlertManagersForStuckOrdersAsync(stillQueued, context, emailService, ct);
    }

    /// <summary>
    /// Finds orders that "just crossed" <see cref="StuckThreshold"/> in this tick window
    /// and emails all branch managers once so they can intervene.
    /// "Just crossed" = waiting between StuckThreshold and StuckThreshold + _interval,
    /// ensuring each order triggers exactly one alert notification.
    /// </summary>
    private async Task AlertManagersForStuckOrdersAsync(
        List<decorativeplant_be.Domain.Entities.OrderHeader> stillQueued,
        IApplicationDbContext context,
        IEmailService emailService,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var alertWindowStart = now - StuckThreshold - _interval;
        var alertWindowEnd   = now - StuckThreshold;

        var stuckOrders = stillQueued
            .Where(o =>
            {
                var anchor = o.ConfirmedAt ?? o.CreatedAt;
                return anchor.HasValue
                    && anchor.Value >= alertWindowStart
                    && anchor.Value <= alertWindowEnd;
            })
            .ToList();

        if (stuckOrders.Count == 0) return;

        // Group by branch so we send one email per branch per tick
        var byBranch = stuckOrders
            .GroupBy(o => o.OrderItems?.FirstOrDefault()?.BranchId)
            .Where(g => g.Key.HasValue);

        foreach (var group in byBranch)
        {
            var branchId = group.Key!.Value;

            var branchName = await context.Branches
                .Where(b => b.Id == branchId)
                .Select(b => b.Name)
                .FirstOrDefaultAsync(ct) ?? "your branch";

            // Find branch managers
            var managers = await (
                from sa in context.StaffAssignments
                join u in context.UserAccounts on sa.StaffId equals u.Id
                where sa.BranchId == branchId
                   && u.Role == "branch_manager"
                   && u.IsActive
                   && u.Email != null
                select new { u.Email, u.DisplayName }
            ).ToListAsync(ct);

            if (managers.Count == 0)
            {
                _logger.LogWarning(
                    "OrderAssignmentQueueJob: Stuck orders at branch {BranchId} but no branch_manager to notify.", branchId);
                continue;
            }

            var orderCount = group.Count();
            var orderListHtml = string.Concat(group.Select(o =>
                $"<li><strong>{WebUtility.HtmlEncode(o.OrderCode ?? o.Id.ToString())}</strong> — waiting since {(o.ConfirmedAt ?? o.CreatedAt)?.ToString("HH:mm UTC")}</li>"));

            foreach (var manager in managers)
            {
                try
                {
                    var greeting = string.IsNullOrWhiteSpace(manager.DisplayName)
                        ? "Hello,"
                        : $"Hello <strong>{WebUtility.HtmlEncode(manager.DisplayName)}</strong>,";

                    var bodyHtml =
                        $"<div style='font-family:sans-serif;max-width:600px;color:#1f2937;'>" +
                        $"<p>{greeting}</p>" +
                        $"<p>⚠️ <strong>{orderCount} order(s)</strong> at <strong>{WebUtility.HtmlEncode(branchName)}</strong> have been waiting in the queue for over {StuckThreshold.TotalMinutes:0} minutes without being assigned to a fulfillment staff.</p>" +
                        $"<p>This may mean all staff are at capacity or no fulfillment staff is currently active at this branch.</p>" +
                        $"<ul>{orderListHtml}</ul>" +
                        $"<p>Please log in to the dashboard to manually assign these orders or check staff availability.</p>" +
                        $"</div>";

                    var plain =
                        $"Hi {manager.DisplayName ?? "manager"},\n\n" +
                        $"{orderCount} order(s) at {branchName} have been unassigned for over {StuckThreshold.TotalMinutes:0} minutes.\n" +
                        $"Orders: {string.Join(", ", group.Select(o => o.OrderCode ?? o.Id.ToString()))}\n\n" +
                        "Please log in to the dashboard to manually assign or check staff availability.";

                    await emailService.SendAsync(new EmailMessage
                    {
                        To = manager.Email!,
                        ToName = manager.DisplayName,
                        Subject = $"[Action required] {orderCount} unassigned order(s) waiting at {branchName}",
                        BodyPlainText = plain,
                        BodyHtml = bodyHtml,
                    }, ct);

                    _logger.LogInformation(
                        "OrderAssignmentQueueJob: Stuck-order alert sent to manager {Email} for branch {BranchId} ({Count} orders).",
                        manager.Email, branchId, orderCount);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "OrderAssignmentQueueJob: Failed to alert manager {Email}.", manager.Email);
                }
            }
        }
    }
}
