using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Quota;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace decorativeplant_be.Infrastructure.Services;

public class QuotaService : IQuotaService
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;
    private readonly IDistributedCache _cache;
    private const int CacheTTLMinutes = 5;

    // Default quotas by plan type
    private static readonly Dictionary<string, Dictionary<string, (int Limit, string Period)>> DefaultQuotas = new()
    {
        ["Free"] = new Dictionary<string, (int, string)>
        {
            ["ai_diagnosis"] = (5, "Monthly"),
            ["plant_count"] = (5, "Monthly"),
            ["care_guide"] = (10, "Monthly"),
            ["ai_recommendation"] = (3, "Monthly"),
            ["export_report"] = (0, "Monthly")
        },
        ["Premium"] = new Dictionary<string, (int, string)>
        {
            ["ai_diagnosis"] = (999999, "Unlimited"),
            ["plant_count"] = (999999, "Unlimited"),
            ["care_guide"] = (999999, "Unlimited"),
            ["ai_recommendation"] = (999999, "Unlimited"),
            ["export_report"] = (50, "Monthly")
        }
    };

    public QuotaService(
        IRepositoryFactory repositoryFactory,
        IUnitOfWork unitOfWork,
        IApplicationDbContext context,
        IDistributedCache cache)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
        _context = context;
        _cache = cache;
    }

    public async Task<ConsumeQuotaResponse> ConsumeQuotaAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default)
    {
        var repository = _repositoryFactory.CreateRepository<FeatureUsageQuota>();

        // Use execution strategy to handle retries with transactions
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var quota = await repository.FirstOrDefaultAsync(
                q => q.UserId == userId && q.FeatureKey == featureKey,
                cancellationToken);

            if (quota == null)
            {
                throw new NotFoundException($"Quota not found for feature: {featureKey}");
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                var now = DateTime.UtcNow;

                // Reset quota if period has ended
                if (quota.PeriodEndDate < now && quota.QuotaPeriod != "Unlimited")
                {
                    quota.QuotaUsed = 0;
                    quota.PeriodStartDate = now;
                    quota.PeriodEndDate = quota.QuotaPeriod == "Monthly" ? now.AddMonths(1) : now.AddYears(1);
                    quota.UpdatedAt = now;
                }

                // Check if quota is exceeded
                if (quota.QuotaUsed >= quota.QuotaLimit)
                {
                    // Return 429 response data
                    return new ConsumeQuotaResponse
                    {
                        FeatureKey = quota.FeatureKey,
                        QuotaUsed = quota.QuotaUsed,
                        QuotaLimit = quota.QuotaLimit,
                        RemainingCount = 0,
                        PeriodEndDate = quota.PeriodEndDate,
                        Message = "Quota exceeded"
                    };
                }

                // Increment quota usage
                quota.QuotaUsed++;
                quota.UpdatedAt = now;

                await repository.UpdateAsync(quota, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // Invalidate cache
                await InvalidateQuotaCacheAsync(userId);

                return new ConsumeQuotaResponse
                {
                    FeatureKey = quota.FeatureKey,
                    QuotaUsed = quota.QuotaUsed,
                    QuotaLimit = quota.QuotaLimit,
                    RemainingCount = quota.QuotaLimit - quota.QuotaUsed,
                    PeriodEndDate = quota.PeriodEndDate,
                    Message = "Quota consumed successfully"
                };
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    public async Task<List<QuotaStatusDto>> GetMyQuotasAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"quota:{userId}";

        // Try to get from cache
        var cachedData = await _cache.GetStringAsync(cacheKey, cancellationToken);
        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<List<QuotaStatusDto>>(cachedData) ?? new List<QuotaStatusDto>();
        }

        // Get from database
        var quotas = await _context.FeatureUsageQuotas
            .Where(q => q.UserId == userId)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var result = quotas.Select(q =>
        {
            // Auto-reset if period ended
            if (q.PeriodEndDate < now && q.QuotaPeriod != "Unlimited")
            {
                q.QuotaUsed = 0;
                q.PeriodStartDate = now;
                q.PeriodEndDate = q.QuotaPeriod == "Monthly" ? now.AddMonths(1) : now.AddYears(1);
            }

            var remaining = Math.Max(0, q.QuotaLimit - q.QuotaUsed);
            var isNearLimit = q.QuotaLimit > 0 && ((double)q.QuotaUsed / q.QuotaLimit) >= 0.8;

            return new QuotaStatusDto
            {
                FeatureKey = q.FeatureKey,
                QuotaLimit = q.QuotaLimit,
                QuotaUsed = q.QuotaUsed,
                RemainingCount = remaining,
                QuotaPeriod = q.QuotaPeriod,
                PeriodEndDate = q.PeriodEndDate,
                IsNearLimit = isNearLimit
            };
        }).ToList();

        // Cache the result
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CacheTTLMinutes)
        };
        await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), cacheOptions, cancellationToken);

        return result;
    }

    public async Task<CheckQuotaResponse> CheckQuotaAsync(Guid userId, string featureKey, CancellationToken cancellationToken = default)
    {
        var quota = await _context.FeatureUsageQuotas
            .FirstOrDefaultAsync(q => q.UserId == userId && q.FeatureKey == featureKey, cancellationToken);

        if (quota == null)
        {
            return new CheckQuotaResponse
            {
                CanUse = false,
                Remaining = 0,
                LimitReached = true
            };
        }

        var now = DateTime.UtcNow;

        // Auto-reset if period ended
        if (quota.PeriodEndDate < now && quota.QuotaPeriod != "Unlimited")
        {
            quota.QuotaUsed = 0;
        }

        var remaining = Math.Max(0, quota.QuotaLimit - quota.QuotaUsed);
        var canUse = remaining > 0;
        var limitReached = quota.QuotaUsed >= quota.QuotaLimit;

        return new CheckQuotaResponse
        {
            CanUse = canUse,
            Remaining = remaining,
            LimitReached = limitReached
        };
    }

    public async Task<List<QuotaSummaryDto>> GetAdminSummaryAsync(CancellationToken cancellationToken = default)
    {
        var quotas = await _context.FeatureUsageQuotas
            .Where(q => q.QuotaLimit > 0)
            .ToListAsync(cancellationToken);

        // Filter quotas where usage >= 80%
        var nearLimit = quotas
            .Where(q => ((double)q.QuotaUsed / q.QuotaLimit) >= 0.8)
            .GroupBy(q => q.FeatureKey)
            .Select(g => new QuotaSummaryDto
            {
                FeatureKey = g.Key,
                AffectedUserCount = g.Count(),
                AverageUsagePercent = g.Average(q => (double)q.QuotaUsed / q.QuotaLimit * 100)
            })
            .OrderByDescending(s => s.AffectedUserCount)
            .ToList();

        return nearLimit;
    }

    public async Task SeedDefaultQuotaForUserAsync(Guid userId, string planType, CancellationToken cancellationToken = default)
    {
        var repository = _repositoryFactory.CreateRepository<FeatureUsageQuota>();

        // Determine which plan quotas to use
        var normalizedPlanType = planType.Contains("Premium", StringComparison.OrdinalIgnoreCase) ? "Premium" : "Free";

        if (!DefaultQuotas.TryGetValue(normalizedPlanType, out var quotas))
        {
            quotas = DefaultQuotas["Free"]; // Default to Free if plan not recognized
        }

        Console.WriteLine($"[QuotaService] Seeding quotas for user {userId}, plan: {normalizedPlanType}");

        var now = DateTime.UtcNow;

        // Delete existing quotas for this user (including soft-deleted ones)
        var existingQuotas = await _context.FeatureUsageQuotas
            .IgnoreQueryFilters() // Important: include soft-deleted records
            .Where(q => q.UserId == userId)
            .ToListAsync(cancellationToken);

        Console.WriteLine($"[QuotaService] Found {existingQuotas.Count} existing quota records to delete");

        foreach (var existing in existingQuotas)
        {
            _context.FeatureUsageQuotas.Remove(existing); // Hard delete
        }

        // Seed new quotas
        Console.WriteLine($"[QuotaService] Creating {quotas.Count} new quota records");

        foreach (var (featureKey, (limit, period)) in quotas)
        {
            var quota = new FeatureUsageQuota
            {
                UserId = userId,
                FeatureKey = featureKey,
                QuotaLimit = limit,
                QuotaUsed = 0,
                QuotaPeriod = period,
                PeriodStartDate = now,
                PeriodEndDate = period == "Monthly" ? now.AddMonths(1) : period == "Yearly" ? now.AddYears(1) : now.AddYears(100),
                CreatedAt = now
            };

            await repository.AddAsync(quota, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        Console.WriteLine($"[QuotaService] Successfully saved {quotas.Count} quota records");

        // Invalidate cache
        await InvalidateQuotaCacheAsync(userId);
    }

    public async Task InvalidateQuotaCacheAsync(Guid userId)
    {
        var cacheKey = $"quota:{userId}";
        await _cache.RemoveAsync(cacheKey);
    }
}
