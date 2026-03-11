using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Analytics;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.API.Controllers;

/// <summary>
/// Analytics endpoints for admin users.
/// </summary>
[ApiController]
[Route("api/analytics")]
[Authorize(Roles = "admin")]
public class AnalyticsController : BaseController
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IQuotaService _quotaService;
    private readonly ISubscriptionService _subscriptionService;

    public AnalyticsController(
        IAnalyticsService analyticsService,
        IQuotaService quotaService,
        ISubscriptionService subscriptionService)
    {
        _analyticsService = analyticsService;
        _quotaService = quotaService;
        _subscriptionService = subscriptionService;
    }

    /// <summary>
    /// Gets subscription conversion statistics.
    /// </summary>
    /// <param name="from">Start date (inclusive). Defaults to 30 days ago.</param>
    /// <param name="to">End date (inclusive). Defaults to now.</param>
    [HttpGet("subscription/conversion")]
    public async Task<ActionResult<ApiResponse<ConversionStatsDto>>> GetConversionStats(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var stats = await _analyticsService.GetConversionStatsAsync(from, to);
        return Ok(ApiResponse<ConversionStatsDto>.SuccessResponse(stats, "Conversion statistics retrieved successfully."));
    }

    /// <summary>
    /// Gets quota usage statistics grouped by feature.
    /// Results are cached in Redis for 1 hour.
    /// </summary>
    /// <param name="minUsagePercent">Minimum usage percentage to filter (0-100). Default is 0 to show all quotas.</param>
    [HttpGet("subscription/quota-incidents")]
    public async Task<ActionResult<ApiResponse<List<QuotaIncidentDto>>>> GetQuotaIncidents(
        [FromQuery] decimal minUsagePercent = 0)
    {
        if (minUsagePercent < 0 || minUsagePercent > 100)
        {
            return BadRequest(ApiResponse<List<QuotaIncidentDto>>.ErrorResponse("minUsagePercent must be between 0 and 100."));
        }

        var incidents = await _analyticsService.GetQuotaIncidentsAsync(minUsagePercent);
        return Ok(ApiResponse<List<QuotaIncidentDto>>.SuccessResponse(incidents, "Quota incidents retrieved successfully."));
    }

    /// <summary>
    /// Debug endpoint: Gets raw quota data count for verification.
    /// </summary>
    [HttpGet("subscription/quota-debug")]
    public async Task<ActionResult<ApiResponse<object>>> GetQuotaDebug()
    {
        // Direct database check - bypass all filters and caching
        var userId = Guid.Parse("34b423fc-27cb-4c65-8f9c-8d9227b15f7e");
        var rawQuotas = await _quotaService.GetMyQuotasAsync(userId);

        var totalRecords = await _quotaService.GetAdminSummaryAsync();
        var allQuotas = await _analyticsService.GetQuotaIncidentsAsync(0);
        var highUsage = await _analyticsService.GetQuotaIncidentsAsync(80);

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            TotalFeatureGroups = allQuotas.Count,
            HighUsageFeatures = highUsage.Count,
            AllQuotas = allQuotas,
            AdminSummary = totalRecords,
            RawQuotasForTestUser = rawQuotas,
            TestUserId = userId,
            Message = "Check RawQuotasForTestUser to see if data exists for the specific user"
        }, "Debug info retrieved successfully."));
    }

    /// <summary>
    /// Admin endpoint: Clears all analytics Redis cache.
    /// </summary>
    [HttpPost("cache/clear")]
    public async Task<ActionResult<ApiResponse<object>>> ClearAnalyticsCache()
    {
        await _analyticsService.ClearCacheAsync();

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            Message = "Analytics cache cleared. Next request will fetch fresh data from database."
        }, "Cache cleared successfully."));
    }

    /// <summary>
    /// Admin endpoint: Seeds quotas for all users who have subscriptions but no quota records.
    /// Includes force option to clear cache and reseed.
    /// </summary>
    [HttpPost("subscription/seed-quotas")]
    public async Task<ActionResult<ApiResponse<object>>> SeedQuotasForAllUsers([FromQuery] bool force = false)
    {
        var stats = await _analyticsService.GetConversionStatsAsync();
        var totalUsers = stats.TotalUsers;
        var seededCount = 0;
        var skippedCount = 0;
        var errors = new List<string>();

        // Get all users with active subscriptions
        var allSubscriptions = await _subscriptionService.GetAllAsync(1, 1000, null, "Active");

        foreach (var subscription in allSubscriptions.Items)
        {
            try
            {
                if (force)
                {
                    // Clear cache first when forcing
                    await _quotaService.InvalidateQuotaCacheAsync(subscription.UserId);
                }

                // Always seed - SeedDefaultQuotaForUserAsync handles deletion of existing records
                await _quotaService.SeedDefaultQuotaForUserAsync(subscription.UserId, subscription.PlanType);
                seededCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"User {subscription.UserId}: {ex.Message}");
            }
        }

        return Ok(ApiResponse<object>.SuccessResponse(new
        {
            TotalUsers = totalUsers,
            ActiveSubscriptions = allSubscriptions.Items.Count,
            QuotasSeeded = seededCount,
            Errors = errors,
            Message = seededCount > 0
                ? $"Successfully seeded quotas for {seededCount} users"
                : "No users found to seed"
        }, "Quota seeding completed."));
    }
}

