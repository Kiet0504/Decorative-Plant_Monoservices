using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Subscription;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController : BaseController
{
    private readonly ISubscriptionService _subscriptionService;

    public SubscriptionsController(ISubscriptionService subscriptionService)
    {
        _subscriptionService = subscriptionService;
    }

    /// <summary>
    /// Gets the current user's active subscription.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> GetMySubscription()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<SubscriptionDto>.ErrorResponse("User not authenticated.", statusCode: 401));
        }

        var subscription = await _subscriptionService.GetCurrentSubscriptionAsync(userId.Value);

        if (subscription == null)
        {
            return NotFound(ApiResponse<SubscriptionDto>.ErrorResponse("No active subscription found.", statusCode: 404));
        }

        return Ok(ApiResponse<SubscriptionDto>.SuccessResponse(subscription, "Subscription retrieved successfully."));
    }

    /// <summary>
    /// Creates a new subscription for the current user.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> CreateSubscription([FromBody] CreateSubscriptionRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<SubscriptionDto>.ErrorResponse("User not authenticated.", statusCode: 401));
        }

        var subscription = await _subscriptionService.CreateSubscriptionAsync(userId.Value, request);
        return Ok(ApiResponse<SubscriptionDto>.SuccessResponse(subscription, "Subscription created successfully."));
    }

    /// <summary>
    /// Cancels the current user's active subscription.
    /// </summary>
    [HttpPut("cancel")]
    public async Task<ActionResult<ApiResponse<object>>> CancelSubscription([FromBody] CancelSubscriptionRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("User not authenticated.", statusCode: 401));
        }

        await _subscriptionService.CancelSubscriptionAsync(userId.Value, request);
        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Subscription cancelled successfully."));
    }

    /// <summary>
    /// Upgrades the current user's subscription from Free to Premium.
    /// </summary>
    [HttpPost("upgrade")]
    [Authorize(Roles = "customer")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> UpgradeSubscription([FromBody] UpgradeSubscriptionRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<SubscriptionDto>.ErrorResponse("User not authenticated.", statusCode: 401));
        }

        var subscription = await _subscriptionService.UpgradeSubscriptionAsync(userId.Value, request);
        return Ok(ApiResponse<SubscriptionDto>.SuccessResponse(subscription, "Subscription upgraded successfully."));
    }

    /// <summary>
    /// Gets a specific user's subscription by user ID (Admin only).
    /// </summary>
    [HttpGet("{userId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<SubscriptionDto>>> GetUserSubscription(Guid userId)
    {
        var subscription = await _subscriptionService.GetSubscriptionByUserIdAsync(userId);

        if (subscription == null)
        {
            return NotFound(ApiResponse<SubscriptionDto>.ErrorResponse("No subscription found for this user.", statusCode: 404));
        }

        return Ok(ApiResponse<SubscriptionDto>.SuccessResponse(subscription, "Subscription retrieved successfully."));
    }

    /// <summary>
    /// Gets all subscriptions with pagination and filtering (Admin only).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<PagedResult<SubscriptionDto>>>> GetAllSubscriptions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? planType = null,
        [FromQuery] string? status = null)
    {
        if (page < 1)
        {
            return BadRequest(ApiResponse<PagedResult<SubscriptionDto>>.ErrorResponse("Page must be greater than 0."));
        }

        if (pageSize < 1 || pageSize > 100)
        {
            return BadRequest(ApiResponse<PagedResult<SubscriptionDto>>.ErrorResponse("PageSize must be between 1 and 100."));
        }

        var result = await _subscriptionService.GetAllAsync(page, pageSize, planType, status);
        return Ok(ApiResponse<PagedResult<SubscriptionDto>>.SuccessResponse(result, "Subscriptions retrieved successfully."));
    }
}
