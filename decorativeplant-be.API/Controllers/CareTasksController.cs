using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Cultivation.Commands;
using decorativeplant_be.Application.Features.Cultivation.DTOs;
using decorativeplant_be.Application.Features.Cultivation.Queries;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Microsoft.Extensions.Logging;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/inventory/care-tasks")]
[Authorize]
public class CareTasksController : BaseController
{
    /// <summary>
    /// Get list of care tasks with filtering.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "admin,branch_manager,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<PagedResultDto<BatchCareTaskDto>>>> GetCareTasks([FromQuery] string? status, [FromQuery] string? searchTerm, [FromQuery] string? sortOrder, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var query = new GetBatchCareTasksQuery { Status = status, SearchTerm = searchTerm, SortOrder = sortOrder, Page = page, PageSize = pageSize };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResultDto<BatchCareTaskDto>>.SuccessResponse(result, "Care tasks retrieved."));
    }




    /// <summary>
    /// Get summary of pending tasks for dashboard.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Roles = "admin,branch_manager,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<BatchCareTasksSummary>>> GetSummary()
    {
        var query = new GetBatchCareTasksSummaryQuery();
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<BatchCareTasksSummary>.SuccessResponse(result, "Summary retrieved."));
    }

    /// <summary>
    /// Get detail of a specific care task.
    /// </summary>
    [HttpGet("{id}")]
    [Authorize(Roles = "admin,branch_manager,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<BatchCareTaskDetailDto>>> GetTaskDetail(Guid id)
    {
        var query = new GetBatchCareTaskByIdQuery { Id = id };
        var result = await Mediator.Send(query);
        if (result == null) return NotFound(ApiResponse<BatchCareTaskDetailDto>.ErrorResponse("Task not found.", statusCode: 404));
        return Ok(ApiResponse<BatchCareTaskDetailDto>.SuccessResponse(result, "Task detail retrieved."));
    }

    /// <summary>
    /// Create a new care task (scheduled maintenance).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "admin,branch_manager,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<Guid>>> CreateTask([FromBody] CreateBatchCareTaskCommand command)
    {
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<Guid>.SuccessResponse(result, "Care task created successfully.", 201));
    }

    /// <summary>
    /// Mark a care task as completed.
    /// </summary>
    [HttpPost("{id}/resolve")]
    [Authorize(Roles = "admin,branch_manager,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<bool>>> ResolveTask(Guid id)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized(ApiResponse<bool>.ErrorResponse("User not identified."));

        var command = new ResolveBatchCareTaskCommand 
        { 
            Id = id, 
            PerformedBy = userId.Value 
        };
        var result = await Mediator.Send(command);
        if (!result) return NotFound(ApiResponse<bool>.ErrorResponse("Task not found.", statusCode: 404));
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Task marked as completed."));
    }
}
