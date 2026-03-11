using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.BackgroundJobs;

public class MonthlyQuotaResetJob : IHostedService, IDisposable
{
    private readonly ILogger<MonthlyQuotaResetJob> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private Timer? _timer;

    public MonthlyQuotaResetJob(
        ILogger<MonthlyQuotaResetJob> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monthly Quota Reset Job is starting.");

        // Calculate the next run time (1st of next month at 00:05 UTC)
        var now = DateTime.UtcNow;
        var nextRun = new DateTime(now.Year, now.Month, 1, 0, 5, 0, DateTimeKind.Utc).AddMonths(1);

        // If we're past the 1st at 00:05 this month, schedule for next month
        if (now > new DateTime(now.Year, now.Month, 1, 0, 5, 0, DateTimeKind.Utc))
        {
            nextRun = new DateTime(now.Year, now.Month, 1, 0, 5, 0, DateTimeKind.Utc).AddMonths(1);
        }
        else
        {
            // If we haven't reached the 1st at 00:05 this month yet, schedule for this month
            nextRun = new DateTime(now.Year, now.Month, 1, 0, 5, 0, DateTimeKind.Utc);
        }

        var initialDelay = nextRun - now;
        _logger.LogInformation("Next quota reset scheduled for: {NextRun} UTC (in {Hours} hours)", nextRun, initialDelay.TotalHours);

        // Set up timer to run monthly
        _timer = new Timer(
            DoWork,
            null,
            initialDelay,
            TimeSpan.FromDays(30) // Approximate monthly interval
        );

        return Task.CompletedTask;
    }

    private async void DoWork(object? state)
    {
        _logger.LogInformation("Monthly Quota Reset Job is executing...");

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

            var now = DateTime.UtcNow;

            // Query all quotas where period has ended
            var expiredQuotas = await context.FeatureUsageQuotas
                .Where(q => q.PeriodEndDate <= now && q.QuotaPeriod != "Unlimited")
                .ToListAsync();

            _logger.LogInformation("Found {Count} quotas to reset.", expiredQuotas.Count);

            // Track affected users for cache invalidation
            var affectedUserIds = expiredQuotas.Select(q => q.UserId).Distinct().ToHashSet();

            // Process in batches of 100
            const int batchSize = 100;
            int totalProcessed = 0;

            for (int i = 0; i < expiredQuotas.Count; i += batchSize)
            {
                var batch = expiredQuotas.Skip(i).Take(batchSize).ToList();

                foreach (var quota in batch)
                {
                    quota.QuotaUsed = 0;
                    quota.PeriodStartDate = now;
                    quota.PeriodEndDate = quota.QuotaPeriod == "Monthly" ? now.AddMonths(1) : now.AddYears(1);
                    quota.UpdatedAt = now;
                }

                await context.SaveChangesAsync();
                totalProcessed += batch.Count;

                _logger.LogInformation("Processed batch {BatchNumber}/{TotalBatches}, Total: {TotalProcessed}/{Total}",
                    (i / batchSize) + 1,
                    (expiredQuotas.Count + batchSize - 1) / batchSize,
                    totalProcessed,
                    expiredQuotas.Count);
            }

            // Invalidate Redis cache for all affected users
            foreach (var userId in affectedUserIds)
            {
                var cacheKey = $"quota:{userId}";
                await cache.RemoveAsync(cacheKey);
            }

            _logger.LogInformation("Monthly quota reset completed. Affected records: {Count}, Affected users: {UserCount}",
                totalProcessed, affectedUserIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during monthly quota reset: {Message}", ex.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monthly Quota Reset Job is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
