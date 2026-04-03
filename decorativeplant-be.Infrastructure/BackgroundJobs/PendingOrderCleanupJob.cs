using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace decorativeplant_be.Infrastructure.BackgroundJobs;

public class PendingOrderCleanupJob : BackgroundService
{
    private readonly ILogger<PendingOrderCleanupJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
    private readonly TimeSpan _expirationThreshold = TimeSpan.FromMinutes(30);

    public PendingOrderCleanupJob(
        ILogger<PendingOrderCleanupJob> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pending Order Cleanup Job is starting.");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await DoWorkAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred during pending order cleanup.");
            }

            await Task.Delay(_checkInterval, cancellationToken);
        }
    }

    private async Task DoWorkAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var expirationTime = DateTime.UtcNow.Subtract(_expirationThreshold);

        // Find pending orders older than the threshold
        var expiredOrders = await context.OrderHeaders
            .Include(o => o.OrderItems)
            .Where(o => o.Status == "pending" && o.CreatedAt <= expirationTime)
            .ToListAsync(cancellationToken);

        if (!expiredOrders.Any())
        {
            return;
        }

        _logger.LogInformation($"Found {expiredOrders.Count} pending orders to expire.");

        foreach (var order in expiredOrders)
        {
            order.Status = "expired";
            
            // Add note about expiration
            var notes = new Dictionary<string, object?>();
            if (order.Notes != null)
            {
                foreach (var p in order.Notes.RootElement.EnumerateObject())
                {
                    notes[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.GetRawText();
                }
            }
            notes["cancellation_reason"] = "Auto-expired due to payment timeout.";
            order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));

            // Restore Stock
            if (order.OrderItems != null)
            {
                foreach (var item in order.OrderItems)
                {
                    if (item.BatchId.HasValue)
                    {
                        var stock = await context.BatchStocks
                            .FirstOrDefaultAsync(s => s.BatchId == item.BatchId, cancellationToken);
                        
                        if (stock != null && stock.Quantities != null)
                        {
                            var root = stock.Quantities.RootElement;
                            var total = root.TryGetProperty("quantity", out var t) ? t.GetInt32() : 0;
                            var reserved = root.TryGetProperty("reserved_quantity", out var r) ? r.GetInt32() : 0;
                            var available = root.TryGetProperty("available_quantity", out var a) ? a.GetInt32() : 0;
                            
                            // Deduct from reserved and add back to available
                            var q = item.Quantity;
                            reserved -= q;
                            if (reserved < 0) reserved = 0;
                            available += q;
                            
                            stock.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(new
                            {
                                quantity = total,
                                reserved_quantity = reserved,
                                available_quantity = available
                            }));

                            _logger.LogInformation(
                                "Restored {Qty} units for Order {OrderCode}, BatchId {BatchId}, BranchId {BranchId}",
                                q, order.OrderCode, item.BatchId, item.BranchId);
                        }
                    }
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation($"Successfully expired {expiredOrders.Count} orders and restored stock.");
    }
}
