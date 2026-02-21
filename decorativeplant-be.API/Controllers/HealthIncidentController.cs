using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.HealthCheck.Commands;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Application.Features.HealthCheck.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/health-incidents")]
[Authorize]
public class HealthIncidentController : BaseController
{
    /// <summary>
    /// Report a new health incident for a plant batch.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<ActionResult<ApiResponse<HealthIncidentDto>>> Report([FromBody] ReportHealthIncidentCommand command)
    {
        command.PerformedBy = GetUserId();
        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<HealthIncidentDto>.SuccessResponse(result, "Health incident reported.", 201));
    }

    /// <summary>
    /// Update treatment details, cost, and recovery status for an incident.
    /// </summary>
    [HttpPut("{id}/treatment")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<ActionResult<ApiResponse<HealthIncidentDto>>> UpdateTreatment(Guid id, [FromBody] UpdateHealthIncidentTreatmentCommand command)
    {
        if (id != command.Id) return BadRequest(ApiResponse<HealthIncidentDto>.ErrorResponse("ID mismatch."));
        
        command.PerformedBy = GetUserId();
        var result = await Mediator.Send(command);
        return Ok(ApiResponse<HealthIncidentDto>.SuccessResponse(result, "Treatment updated."));
    }

    /// <summary>
    /// Get the health history (incidents) for a specific batch.
    /// </summary>
    [HttpGet("batches/{batchId}/history")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<ActionResult<ApiResponse<List<HealthIncidentDto>>>> GetBatchHistory(Guid batchId)
    {
        var result = await Mediator.Send(new GetBatchHealthHistoryQuery { BatchId = batchId });
        return Ok(ApiResponse<List<HealthIncidentDto>>.SuccessResponse(result, "Batch health history retrieved."));
    }
}
