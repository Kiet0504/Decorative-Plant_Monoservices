using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
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

        _logger.LogInformation("Found {Count} pending orders to expire.", expiredOrders.Count);

        int successCount = 0;

        // Process each order in its own transaction for isolation
        foreach (var order in expiredOrders)
        {
            var strategy = context.Database.CreateExecutionStrategy();
            try
            {
                await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        order.Status = "expired";

                        // Add note about expiration
                        var notes = new Dictionary<string, object?>();
                        if (order.Notes != null)
                        {
                            foreach (var p in order.Notes.RootElement.EnumerateObject())
                            {
                                notes[p.Name] = p.Value.ValueKind == JsonValueKind.String
                                    ? p.Value.GetString()
                                    : p.Value.GetRawText();
                            }
                        }
                        notes["cancellation_reason"] = "Auto-expired due to payment timeout.";
                        notes["expired_at"] = DateTime.UtcNow.ToString("o");
                        order.Notes = JsonDocument.Parse(JsonSerializer.Serialize(notes));

                        // Restore stock with pessimistic locking via StockService
                        if (order.OrderItems != null)
                        {
                            var stockService = scope.ServiceProvider.GetRequiredService<IStockService>();
                            await stockService.RestoreOrderStockAsync(order.OrderItems, cancellationToken);
                        }

                        await context.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        successCount++;
                    }
                    catch
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to expire order {OrderCode}.", order.OrderCode);
            }
        }

        _logger.LogInformation("Successfully expired {Count}/{Total} orders and restored stock.", successCount, expiredOrders.Count);
    }
}
