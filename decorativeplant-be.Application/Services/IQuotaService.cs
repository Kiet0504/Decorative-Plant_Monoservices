using decorativeplant_be.Application.Common.DTOs.Quota;

namespace decorativeplant_be.Application.Services;

public interface IQuotaService
{
    /// <summary>
    /// Consumes one unit of quota for the specified feature. Returns 429 if quota exceeded.
    /// </summary>
    Task<ConsumeQuotaResponse> ConsumeQuotaAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all quota status for the current user (cached in Redis).
    /// </summary>
    Task<List<QuotaStatusDto>> GetMyQuotasAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if user has remaining quota without incrementing usage.
    /// </summary>
    Task<CheckQuotaResponse> CheckQuotaAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets quota summary for admin - features where usage >= 80% of limit.
    /// </summary>
    Task<List<QuotaSummaryDto>> GetAdminSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Seeds default quotas for a user based on their plan type.
    /// </summary>
    Task SeedDefaultQuotaForUserAsync(Guid userId, string planType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the Redis cache for a specific user's quota.
    /// </summary>
    Task InvalidateQuotaCacheAsync(Guid userId);
}
