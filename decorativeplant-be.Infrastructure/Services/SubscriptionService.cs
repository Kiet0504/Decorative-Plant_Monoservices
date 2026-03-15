using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Subscription;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Infrastructure.Services;

public class SubscriptionService : ISubscriptionService
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly IQuotaService _quotaService;

    public SubscriptionService(
        IRepositoryFactory repositoryFactory,
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        IQuotaService quotaService)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
        _context = context;
        _quotaService = quotaService;
    }

    public async Task<SubscriptionDto?> GetCurrentSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var repository = _repositoryFactory.CreateRepository<UserSubscription>();
        var subscription = await repository.FirstOrDefaultAsync(
            s => s.UserId == userId && s.Status == "Active",
            cancellationToken);

        if (subscription == null)
            return null;

        return MapToDto(subscription);
    }

    public async Task<SubscriptionDto> CreateSubscriptionAsync(Guid userId, CreateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var repository = _repositoryFactory.CreateRepository<UserSubscription>();

        // Use execution strategy to handle retries with transactions
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Check for existing active subscription
            var existingActiveSubscription = await repository.FirstOrDefaultAsync(
                s => s.UserId == userId && s.Status == "Active",
                cancellationToken);

            // Begin transaction for state changes
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var now = DateTime.UtcNow;

                // If user has an active Free subscription and is upgrading to Premium, automatically cancel the Free subscription
                if (existingActiveSubscription != null)
                {
                    var isUpgradingFromFree = existingActiveSubscription.PlanType.Contains("Free", StringComparison.OrdinalIgnoreCase)
                                              && request.PlanType.Contains("Premium", StringComparison.OrdinalIgnoreCase);

                    if (isUpgradingFromFree)
                    {
                        // Cancel the existing Free subscription
                        existingActiveSubscription.Status = "Cancelled";
                        existingActiveSubscription.CancelledAt = now;
                        existingActiveSubscription.CancellationReason = "Upgraded to Premium";
                        existingActiveSubscription.UpdatedAt = now;
                        await repository.UpdateAsync(existingActiveSubscription, cancellationToken);
                    }
                    else
                    {
                        // Prevent creating duplicate active subscriptions (e.g., two Premium subscriptions)
                        throw new BadRequestException("User already has an active subscription. Please cancel the current subscription before creating a new one.");
                    }
                }

                DateTime? endAt = null;

                // Calculate end date based on plan type and billing cycle
                if (request.PlanType.Contains("Premium", StringComparison.OrdinalIgnoreCase))
                {
                    endAt = request.BillingCycle switch
                    {
                        "Monthly" => now.AddMonths(1),
                        "Yearly" => now.AddYears(1),
                        _ => throw new BadRequestException("Invalid billing cycle for Premium subscription.")
                    };
                }

                var subscription = new UserSubscription
                {
                    UserId = userId,
                    PlanType = request.PlanType,
                    Status = "Active",
                    StartAt = now,
                    EndAt = endAt,
                    AutoRenew = false,
                    PaymentMethod = request.PaymentMethod,
                    BillingCycle = request.BillingCycle,
                    CreatedAt = now
                };

                await repository.AddAsync(subscription, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // Seed quotas for the new subscription plan
                await _quotaService.SeedDefaultQuotaForUserAsync(userId, request.PlanType, cancellationToken);

                return MapToDto(subscription);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task CancelSubscriptionAsync(Guid userId, CancelSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var repository = _repositoryFactory.CreateRepository<UserSubscription>();

        // Use execution strategy to handle retries with transactions
        var strategy = _context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            var subscription = await repository.FirstOrDefaultAsync(
                s => s.UserId == userId && s.Status == "Active",
                cancellationToken);

            if (subscription == null)
            {
                throw new NotFoundException("No active subscription found for this user.");
            }

            // Begin transaction for state changes
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var now = DateTime.UtcNow;
                var wasPremium = subscription.PlanType.Contains("Premium", StringComparison.OrdinalIgnoreCase);

                // Cancel the current subscription
                subscription.Status = "Cancelled";
                subscription.CancelledAt = now;
                subscription.CancellationReason = request.Reason;
                subscription.UpdatedAt = now;

                await repository.UpdateAsync(subscription, cancellationToken);

                // If cancelling a Premium subscription, automatically create a Free subscription
                if (wasPremium)
                {
                    var freeSubscription = new UserSubscription
                    {
                        UserId = userId,
                        PlanType = "Free",
                        Status = "Active",
                        StartAt = now,
                        EndAt = null, // Free subscription never expires
                        AutoRenew = false,
                        PaymentMethod = null,
                        BillingCycle = null,
                        CreatedAt = now
                    };

                    await repository.AddAsync(freeSubscription, cancellationToken);
                }

                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // If downgraded from Premium to Free, seed Free quotas
                if (wasPremium)
                {
                    await _quotaService.SeedDefaultQuotaForUserAsync(userId, "Free", cancellationToken);
                }
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<PagedResult<SubscriptionDto>> GetAllAsync(int page, int pageSize, string? planType, string? status, CancellationToken cancellationToken = default)
    {
        var query = _context.UserSubscriptions.AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(planType))
        {
            query = query.Where(s => s.PlanType == planType);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(s => s.Status == status);
        }

        // Get total count
        var totalCount = await query.CountAsync(cancellationToken);

        // Apply pagination
        var subscriptions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = subscriptions.Select(MapToDto).ToList();

        return new PagedResult<SubscriptionDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<SubscriptionDto?> GetSubscriptionByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await _context.UserSubscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (subscription == null)
            return null;

        return MapToDto(subscription);
    }

    public async Task CreateFreeSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var repository = _repositoryFactory.CreateRepository<UserSubscription>();

        // Check if user already has a subscription
        var existingSubscription = await repository.FirstOrDefaultAsync(
            s => s.UserId == userId,
            cancellationToken);

        if (existingSubscription != null)
        {
            return; // User already has a subscription, don't create another
        }

        var now = DateTime.UtcNow;
        var subscription = new UserSubscription
        {
            UserId = userId,
            PlanType = "Free",
            Status = "Active",
            StartAt = now,
            EndAt = null, // Free subscription never expires
            AutoRenew = false,
            PaymentMethod = null,
            BillingCycle = null,
            CreatedAt = now
        };

        await repository.AddAsync(subscription, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<SubscriptionDto> UpgradeSubscriptionAsync(Guid userId, UpgradeSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var repository = _repositoryFactory.CreateRepository<UserSubscription>();

        // Use execution strategy to handle retries with transactions
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // Begin transaction for all state changes
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                // 1. Load current user's Active subscription
                var currentSubscription = await repository.FirstOrDefaultAsync(
                    s => s.UserId == userId && s.Status == "Active",
                    cancellationToken);

                // 2. Validate: If no active subscription
                if (currentSubscription == null)
                {
                    throw new BadRequestException("No active subscription found.");
                }

                // 3. Validate: If already Premium
                if (currentSubscription.PlanType.Contains("Premium", StringComparison.OrdinalIgnoreCase))
                {
                    throw new BadRequestException("Already on Premium plan.");
                }

                var now = DateTime.UtcNow;

                // 4. Expire old subscription
                currentSubscription.Status = "Expired";
                currentSubscription.EndAt = now;
                currentSubscription.UpdatedAt = now;
                await repository.UpdateAsync(currentSubscription, cancellationToken);

                // 5. Create new Premium subscription
                DateTime endAt = request.BillingCycle.Equals("Monthly", StringComparison.OrdinalIgnoreCase)
                    ? now.AddMonths(1)
                    : now.AddYears(1);

                var newSubscription = new UserSubscription
                {
                    UserId = userId,
                    PlanType = "Premium",
                    Status = "Active",
                    StartAt = now,
                    EndAt = endAt,
                    AutoRenew = true,
                    PaymentMethod = request.PaymentMethod,
                    AmountPaid = request.AmountPaid,
                    BillingCycle = request.BillingCycle,
                    CreatedAt = now
                };

                await repository.AddAsync(newSubscription, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                // 6. Commit transaction first
                await transaction.CommitAsync(cancellationToken);

                // 7. Seed default quota for Premium plan
                await _quotaService.SeedDefaultQuotaForUserAsync(userId, "Premium", cancellationToken);

                // Note: SeedDefaultQuotaForUserAsync already invalidates Redis cache internally
                // at QuotaService.cs:284

                return MapToDto(newSubscription);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private static SubscriptionDto MapToDto(UserSubscription subscription)
    {
        var now = DateTime.UtcNow;
        var daysRemaining = 0;

        if (subscription.EndAt.HasValue && subscription.Status == "Active")
        {
            var remaining = subscription.EndAt.Value - now;
            daysRemaining = remaining.Days > 0 ? remaining.Days : 0;
        }
        else if (subscription.PlanType.Contains("Free", StringComparison.OrdinalIgnoreCase) && subscription.Status == "Active")
        {
            daysRemaining = int.MaxValue; // Free subscription never expires
        }

        return new SubscriptionDto
        {
            Id = subscription.Id,
            UserId = subscription.UserId,
            PlanType = subscription.PlanType,
            Status = subscription.Status,
            StartAt = subscription.StartAt,
            EndAt = subscription.EndAt,
            AutoRenew = subscription.AutoRenew,
            BillingCycle = subscription.BillingCycle,
            DaysRemaining = daysRemaining
        };
    }
}
