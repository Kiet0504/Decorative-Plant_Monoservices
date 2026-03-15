using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Infrastructure.Data;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.API.Middleware;

/// <summary>
/// Middleware that enforces soft paywall limits on premium features based on usage quotas.
/// Returns HTTP 402 (Payment Required) when monthly quota is exceeded.
/// </summary>
public class SoftPaywallMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<SoftPaywallMiddleware> _logger;

    // Route-to-feature mapping
    private static readonly Dictionary<string, string> RouteFeatureMap = new()
    {
        { "/api/ai/diagnose", FreeTierDefaults.AiDiagnosisFeatureKey },
        { "/api/ai/recommend", FreeTierDefaults.AiRecommendationFeatureKey }
    };

    public SoftPaywallMiddleware(
        RequestDelegate next,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<SoftPaywallMiddleware> logger)
    {
        _next = next;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Step 1: Match routes - only handle POST requests to specific AI endpoints
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        var method = context.Request.Method;

        if (method != "POST" || !RouteFeatureMap.TryGetValue(path, out var featureKey))
        {
            // Not a route we care about - skip middleware
            await _next(context);
            return;
        }

        // Step 2: Extract userId from JWT claim "sub"
        var userIdClaim = context.User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            // Unauthenticated or invalid userId - skip (let auth middleware handle)
            _logger.LogWarning("SoftPaywallMiddleware: No valid userId claim found for {Path}", path);
            await _next(context);
            return;
        }

        // Step 3: Resolve ApplicationDbContext via IServiceScopeFactory
        using var scope = _serviceScopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            // Step 4: Find PremiumFeature by FeatureKey (validate feature exists and is active)
            var premiumFeature = await dbContext.PremiumFeatures
                .FirstOrDefaultAsync(pf => pf.FeatureKey == featureKey && pf.IsActive);

            if (premiumFeature == null)
            {
                _logger.LogWarning(
                    "PremiumFeature not found or inactive for FeatureKey={FeatureKey}",
                    featureKey);
                // Feature doesn't exist or is inactive - proceed without quota check
                await _next(context);
                return;
            }

            // Step 5: Find FeatureUsageQuota for current month
            // Note: DB uses FeatureKey (string) instead of FeatureId (FK) and PeriodDates instead of MonthYear
            var now = DateTime.UtcNow;
            var currentMonthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var currentMonthEnd = currentMonthStart.AddMonths(1).AddSeconds(-1);

            var quota = await dbContext.FeatureUsageQuotas
                .FirstOrDefaultAsync(q =>
                    q.UserId == userId &&
                    q.FeatureKey == featureKey &&
                    q.PeriodStartDate <= now &&
                    q.PeriodEndDate >= now);

            // Step 6: If no quota row found, create new one with free tier defaults
            if (quota == null)
            {
                var defaultQuota = FreeTierDefaults.GetDefaultQuota(featureKey);

                quota = new FeatureUsageQuota
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    FeatureKey = featureKey,
                    QuotaLimit = defaultQuota,
                    QuotaUsed = 0,
                    QuotaPeriod = "Monthly",
                    PeriodStartDate = currentMonthStart,
                    PeriodEndDate = currentMonthEnd,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                dbContext.FeatureUsageQuotas.Add(quota);
                await dbContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Created new quota for UserId={UserId}, FeatureKey={FeatureKey}, Limit={Limit}",
                    userId, featureKey, defaultQuota);
            }

            // Step 7: Check if quota exceeded (UsedCount >= MaxQuota in task spec, but DB uses QuotaUsed/QuotaLimit)
            if (quota.QuotaUsed >= quota.QuotaLimit)
            {
                _logger.LogWarning(
                    "Quota exceeded for UserId={UserId}, FeatureKey={FeatureKey}, Used={Used}, Limit={Limit}",
                    userId, featureKey, quota.QuotaUsed, quota.QuotaLimit);

                // Return HTTP 402 Payment Required
                context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
                context.Response.ContentType = "application/json";

                var errorResponse = new
                {
                    message = "Monthly quota exceeded",
                    featureKey = featureKey,
                    currentUsage = quota.QuotaUsed,
                    quota = quota.QuotaLimit,
                    upgradeUrl = "/api/subscription/plans"
                };

                await context.Response.WriteAsync(
                    JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }));

                return;
            }

            // Step 8: Increment usage count and proceed
            quota.QuotaUsed += 1;
            quota.UpdatedAt = now;
            await dbContext.SaveChangesAsync();

            _logger.LogInformation(
                "Quota incremented for UserId={UserId}, FeatureKey={FeatureKey}, NewUsage={NewUsage}/{Limit}",
                userId, featureKey, quota.QuotaUsed, quota.QuotaLimit);

            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error in SoftPaywallMiddleware for UserId={UserId}, FeatureKey={FeatureKey}",
                userId, featureKey);

            // On error, allow request to proceed (fail open)
            await _next(context);
        }
    }
}
