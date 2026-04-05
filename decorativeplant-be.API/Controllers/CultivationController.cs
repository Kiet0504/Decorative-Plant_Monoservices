using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Cultivation.Commands;
using decorativeplant_be.Application.Features.Cultivation.DTOs;
using decorativeplant_be.Application.Features.Cultivation.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/cultivation")]
[Authorize]
public class CultivationController : BaseController
{
    /// <summary>
    /// Log a cultivation activity (Watering, Fertilizing, Pruning, etc.).
    /// </summary>
    [HttpPost("logs")]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff,fulfillment_staff")]
    public async Task<ActionResult<ApiResponse<CultivationLogDto>>> LogActivity([FromBody] CreateCultivationLogDto dto)
    {
        var command = new LogCultivationActivityCommand
        {
            BatchId = dto.BatchId,
            LocationId = dto.LocationId,
            ActivityType = dto.ActivityType,
            Description = dto.Description,
            Details = dto.Details,
            PerformedAt = dto.PerformedAt,
            PerformedBy = GetUserId() // Helper method from BaseController or derived from claims
        };

        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<CultivationLogDto>.SuccessResponse(result, "Activity logged successfully.", 201));
    }

    /// <summary>
    /// Get cultivation history for a specific batch.
    /// </summary>
    [HttpGet("batches/{batchId}/history")]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<List<CultivationLogDto>>>> GetBatchHistory(Guid batchId)
    {
        var query = new GetBatchCultivationHistoryQuery { BatchId = batchId };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<List<CultivationLogDto>>.SuccessResponse(result, "History retrieved."));
    }
}
