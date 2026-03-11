using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Subscription;

namespace decorativeplant_be.Application.Services;

public interface ISubscriptionService
{
    /// <summary>
    /// Gets the current active subscription for a user.
    /// </summary>
    Task<SubscriptionDto?> GetCurrentSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new subscription for a user. Validates that there is no existing active subscription.
    /// </summary>
    Task<SubscriptionDto> CreateSubscriptionAsync(Guid userId, CreateSubscriptionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels the user's current active subscription.
    /// </summary>
    Task CancelSubscriptionAsync(Guid userId, CancelSubscriptionRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all subscriptions with pagination and filtering (Admin only).
    /// </summary>
    Task<PagedResult<SubscriptionDto>> GetAllAsync(int page, int pageSize, string? planType, string? status, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific user's subscription by user ID (Admin only).
    /// </summary>
    Task<SubscriptionDto?> GetSubscriptionByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a free subscription for a new user during registration.
    /// </summary>
    Task CreateFreeSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upgrades a user's subscription from Free to Premium.
    /// </summary>
    Task<SubscriptionDto> UpgradeSubscriptionAsync(Guid userId, UpgradeSubscriptionRequest request, CancellationToken cancellationToken = default);
}
