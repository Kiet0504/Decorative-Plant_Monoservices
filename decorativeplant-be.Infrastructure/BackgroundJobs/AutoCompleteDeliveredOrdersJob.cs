using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.BackgroundJobs;

/// <summary>
/// Shopee-style auto-close: orders delivered longer than
/// <see cref="_autoCompleteThreshold"/> transition to "completed" on the
/// customer's behalf. Runs every <see cref="_checkInterval"/>.
///
/// Uses the scalar <c>OrderHeader.DeliveredAt</c> column + partial index so
/// the eligibility check is a single indexed range scan rather than a
/// JSONB scan over status_history.
/// </summary>
public class AutoCompleteDeliveredOrdersJob : BackgroundService
{
    private readonly ILogger<AutoCompleteDeliveredOrdersJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _autoCompleteThreshold = TimeSpan.FromHours(24);

    public AutoCompleteDeliveredOrdersJob(
        ILogger<AutoCompleteDeliveredOrdersJob> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Auto-Complete Delivered Orders Job starting (threshold: {Hours}h, interval: {Interval}m).",
            _autoCompleteThreshold.TotalHours, _checkInterval.TotalMinutes);

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

        var cutoff = DateTime.UtcNow.Subtract(_autoCompleteThreshold);

        // Indexed filter (partial index on DeliveredAt WHERE Status='delivered').
        // Passes through the execution strategy so Npgsql retry is safe.
        var candidates = await context.OrderHeaders
            .Where(o => o.Status == OrderStatusMachine.Delivered
                     && o.DeliveredAt != null
                     && o.DeliveredAt <= cutoff)
            .ToListAsync(ct);

        if (candidates.Count == 0) return;

        var auto = 0;
        foreach (var order in candidates)
        {
            try
            {
                OrderStatusMachine.Apply(order, OrderStatusMachine.Completed,
                    changedBy: null,
                    reason: $"Auto-completed {_autoCompleteThreshold.TotalHours:0}h after delivery",
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
}
