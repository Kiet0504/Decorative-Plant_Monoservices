using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Analytics;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Service for analytics and reporting operations.
/// </summary>
public class AnalyticsService : IAnalyticsService
{
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private readonly ILogger<AnalyticsService> _logger;
    private const int QuotaIncidentCacheTTLMinutes = 60; // 1 hour
    private const string QuotaIncidentCacheKey = "analytics:quota-incidents";

    public AnalyticsService(
        IApplicationDbContext context,
        IDistributedCache cache,
        ILogger<AnalyticsService> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ConversionStatsDto> GetConversionStatsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("GetConversionStatsAsync called with from={From}, to={To}", from, to);

            // Default to last 30 days if not specified
            // Ensure DateTime.Kind is UTC for PostgreSQL compatibility
            var fromDate = from.HasValue
                ? DateTime.SpecifyKind(from.Value, DateTimeKind.Utc)
                : DateTime.UtcNow.AddDays(-30);
            var toDate = to.HasValue
                ? DateTime.SpecifyKind(to.Value, DateTimeKind.Utc)
                : DateTime.UtcNow;

            // 1. TotalUsers = count distinct user_ids in user_subscription
            var totalUsers = await _context.UserSubscriptions
                .Select(s => s.UserId)
                .Distinct()
                .CountAsync(cancellationToken);

            _logger.LogInformation("Total users: {TotalUsers}", totalUsers);

            // 2. FreeUsers = count where plan_type = "Free" AND status = "Active"
            var freeUsers = await _context.UserSubscriptions
                .Where(s => s.PlanType == "Free" && s.Status == "Active")
                .CountAsync(cancellationToken);

            _logger.LogInformation("Free users: {FreeUsers}", freeUsers);

            // 3. PremiumUsers = count where plan_type = "Premium" AND status = "Active"
            var premiumUsers = await _context.UserSubscriptions
                .Where(s => s.PlanType == "Premium" && s.Status == "Active")
                .CountAsync(cancellationToken);

            _logger.LogInformation("Premium users: {PremiumUsers}", premiumUsers);

            // 4. ConversionRate = PremiumUsers / TotalUsers * 100
            var conversionRate = totalUsers > 0 ? (decimal)premiumUsers / totalUsers * 100 : 0;

            // 5. UpgradesPerMonth = group by month/year where plan_type="Premium" AND created_at BETWEEN from AND to
            // Use ToList() first to avoid complex GroupBy translation issues with PostgreSQL
            var premiumSubscriptions = await _context.UserSubscriptions
                .Where(s => s.PlanType == "Premium"
                         && s.CreatedAt >= fromDate
                         && s.CreatedAt <= toDate)
                .Select(s => s.CreatedAt)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Premium subscriptions in date range: {Count}", premiumSubscriptions.Count);

            var upgradesPerMonth = premiumSubscriptions
                .GroupBy(createdAt => new { createdAt.Year, createdAt.Month })
                .Select(g => new MonthlyUpgradeDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Count = g.Count()
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            return new ConversionStatsDto
            {
                TotalUsers = totalUsers,
                FreeUsers = freeUsers,
                PremiumUsers = premiumUsers,
                ConversionRate = Math.Round(conversionRate, 2),
                UpgradesPerMonth = upgradesPerMonth
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetConversionStatsAsync: {Message}", ex.Message);
            throw;
        }
    }

    public async Task<List<QuotaIncidentDto>> GetQuotaIncidentsAsync(decimal minUsagePercent = 0, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("GetQuotaIncidentsAsync called with minUsagePercent={MinUsagePercent}", minUsagePercent);

            // Use different cache keys based on filter
            var cacheKey = minUsagePercent > 0
                ? $"{QuotaIncidentCacheKey}:{minUsagePercent}"
                : QuotaIncidentCacheKey;

            // Try to get from cache first
            var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
            if (!string.IsNullOrEmpty(cachedData))
            {
                _logger.LogInformation("Returning cached quota incidents");
                return JsonSerializer.Deserialize<List<QuotaIncidentDto>>(cachedData) ?? new List<QuotaIncidentDto>();
            }

            // Get all quotas from database
            var allQuotas = await _context.FeatureUsageQuotas
                .Where(q => q.QuotaLimit > 0)
                .ToListAsync(cancellationToken);

            _logger.LogInformation("Total quotas found: {Count}", allQuotas.Count);

            // Filter by minimum usage percentage and group by feature
            var minUsageRatio = minUsagePercent / 100;
            var incidents = allQuotas
                .Where(q => (double)q.QuotaUsed / q.QuotaLimit >= (double)minUsageRatio)
                .GroupBy(q => q.FeatureKey)
                .Select(g => new QuotaIncidentDto
                {
                    FeatureKey = g.Key,
                    TotalAffectedUsers = g.Count(),
                    AvgUsagePercent = Math.Round(g.Average(q => (decimal)q.QuotaUsed / q.QuotaLimit * 100), 2),
                    MaxUsagePercent = Math.Round(g.Max(q => (decimal)q.QuotaUsed / q.QuotaLimit * 100), 2)
                })
                .OrderByDescending(i => i.MaxUsagePercent)
                .ThenByDescending(i => i.TotalAffectedUsers)
                .ToList();

            _logger.LogInformation("Quota incidents after filtering: {Count}", incidents.Count);

            // Cache the result for 1 hour
            var cacheOptions = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(QuotaIncidentCacheTTLMinutes)
            };
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(incidents),
                cacheOptions,
                cancellationToken);

            return incidents;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetQuotaIncidentsAsync: {Message}", ex.Message);
            throw;
        }
    }

    public async Task ClearCacheAsync()
    {
        // Clear all possible cache keys
        await _cache.RemoveAsync(QuotaIncidentCacheKey);

        // Clear cache keys with different minUsagePercent values (common ones)
        for (int i = 0; i <= 100; i += 10)
        {
            await _cache.RemoveAsync($"{QuotaIncidentCacheKey}:{i}");
        }

        _logger.LogInformation("Analytics cache cleared");
    }
}
