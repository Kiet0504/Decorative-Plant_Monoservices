using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.BackgroundJobs;

/// <summary>
/// Shopee-style auto-close: orders that have been "delivered" for longer than
/// <see cref="_autoCompleteThreshold"/> are transitioned to "completed" on the
/// customer's behalf. Runs every <see cref="_checkInterval"/>.
/// </summary>
public class AutoCompleteDeliveredOrdersJob : BackgroundService
{
    private readonly ILogger<AutoCompleteDeliveredOrdersJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _autoCompleteThreshold = TimeSpan.FromDays(7);

    public AutoCompleteDeliveredOrdersJob(
        ILogger<AutoCompleteDeliveredOrdersJob> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auto-Complete Delivered Orders Job starting (threshold: {Days}d).",
            _autoCompleteThreshold.TotalDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto-complete job.");
            }
            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task DoWorkAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        // Pick all delivered orders; filter by delivered_at inside the loop
        // because we store it inside JSONB (status_history), not a column.
        var candidates = await context.OrderHeaders
            .Where(o => o.Status == OrderStatusMachine.Delivered)
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        var cutoff = DateTime.UtcNow.Subtract(_autoCompleteThreshold);
        var auto = 0;

        foreach (var order in candidates)
        {
            var deliveredAt = ExtractDeliveredAt(order);
            if (deliveredAt == null || deliveredAt > cutoff) continue;

            try
            {
                OrderStatusMachine.Apply(order, OrderStatusMachine.Completed,
                    changedBy: null,
                    reason: $"Auto-completed {_autoCompleteThreshold.TotalDays:0}d after delivery",
                    source: "AutoCompleteJob");
                auto++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skip auto-complete for {OrderCode}", order.OrderCode);
            }
        }

        if (auto > 0)
        {
            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Auto-completed {Count} delivered order(s).", auto);
        }
    }

    private static DateTime? ExtractDeliveredAt(Domain.Entities.OrderHeader order)
    {
        if (order.Notes == null) return null;
        if (!order.Notes.RootElement.TryGetProperty("status_history", out var hist)) return null;
        if (hist.ValueKind != System.Text.Json.JsonValueKind.Array) return null;

        DateTime? latest = null;
        foreach (var h in hist.EnumerateArray())
        {
            if (!h.TryGetProperty("to", out var to)) continue;
            if (to.GetString() != OrderStatusMachine.Delivered) continue;
            if (!h.TryGetProperty("at", out var at)) continue;
            if (DateTime.TryParse(at.GetString(), out var dt))
            {
                if (latest == null || dt > latest) latest = dt;
            }
        }
        return latest;
    }
}
