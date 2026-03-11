using decorativeplant_be.Application.Common.DTOs.Analytics;

namespace decorativeplant_be.Application.Services;

/// <summary>
/// Service for analytics and reporting operations.
/// </summary>
public interface IAnalyticsService
{
    /// <summary>
    /// Gets subscription conversion statistics for a given date range.
    /// </summary>
    /// <param name="from">Start date (inclusive). Defaults to 30 days ago.</param>
    /// <param name="to">End date (inclusive). Defaults to now.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<ConversionStatsDto> GetConversionStatsAsync(DateTime? from = null, DateTime? to = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets quota usage statistics grouped by feature.
    /// Results are cached in Redis for 1 hour.
    /// </summary>
    /// <param name="minUsagePercent">Minimum usage percentage to filter (0-100). Default is 0 to show all.</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<List<QuotaIncidentDto>> GetQuotaIncidentsAsync(decimal minUsagePercent = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all analytics Redis cache.
    /// </summary>
    Task ClearCacheAsync();
}
