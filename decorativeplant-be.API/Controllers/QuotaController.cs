using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Quota;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/quota")]
[Authorize]
public class QuotaController : BaseController
{
    private readonly IQuotaService _quotaService;

    public QuotaController(IQuotaService quotaService)
    {
        _quotaService = quotaService;
    }

    /// <summary>
    /// Consumes one unit of quota for the specified feature.
    /// </summary>
    [HttpPost("consume")]
    public async Task<ActionResult<ApiResponse<ConsumeQuotaResponse>>> ConsumeQuota([FromBody] ConsumeQuotaRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<ConsumeQuotaResponse>.ErrorResponse("User not authenticated.", statusCode: 401));
        }

        var result = await _quotaService.ConsumeQuotaAsync(userId.Value, request.FeatureKey);

        // Return 429 if quota exceeded
        if (result.Message == "Quota exceeded")
        {
            var errorResponse = ApiResponse<ConsumeQuotaResponse>.ErrorResponse(
                "Quota exceeded. Please upgrade your plan or wait for quota reset.",
                statusCode: 429);
            errorResponse.Data = result;
            return StatusCode(429, errorResponse);
        }

        return Ok(ApiResponse<ConsumeQuotaResponse>.SuccessResponse(result, "Quota consumed successfully."));
    }

    /// <summary>
    /// Gets all quota status for the current user.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<List<QuotaStatusDto>>>> GetMyQuotas()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<List<QuotaStatusDto>>.ErrorResponse("User not authenticated.", statusCode: 401));
        }

        var quotas = await _quotaService.GetMyQuotasAsync(userId.Value);
        return Ok(ApiResponse<List<QuotaStatusDto>>.SuccessResponse(quotas, "Quotas retrieved successfully."));
    }

    /// <summary>
    /// Checks if user has remaining quota without incrementing usage.
    /// </summary>
    [HttpPost("check")]
    public async Task<ActionResult<ApiResponse<CheckQuotaResponse>>> CheckQuota([FromBody] CheckQuotaRequest request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<CheckQuotaResponse>.ErrorResponse("User not authenticated.", statusCode: 401));
        }

        var result = await _quotaService.CheckQuotaAsync(userId.Value, request.FeatureKey);
        return Ok(ApiResponse<CheckQuotaResponse>.SuccessResponse(result, "Quota checked successfully."));
    }

    /// <summary>
    /// Gets quota summary for admin - features where usage >= 80% of limit.
    /// </summary>
    [HttpGet("admin/summary")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<List<QuotaSummaryDto>>>> GetAdminSummary()
    {
        var summary = await _quotaService.GetAdminSummaryAsync();
        return Ok(ApiResponse<List<QuotaSummaryDto>>.SuccessResponse(summary, "Admin summary retrieved successfully."));
    }
}
