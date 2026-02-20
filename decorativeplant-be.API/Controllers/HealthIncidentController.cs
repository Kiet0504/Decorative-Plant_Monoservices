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
    /// Report a new health incident (Pest, Disease, etc.).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<ActionResult<ApiResponse<HealthIncidentDto>>> Report([FromBody] CreateHealthIncidentDto dto)
    {
        var command = new ReportHealthIncidentCommand
        {
            BatchId = dto.BatchId,
            IncidentType = dto.IncidentType,
            Severity = dto.Severity,
            Description = dto.Description,
            ImageUrls = dto.ImageUrls,
            ReportedAt = dto.ReportedAt,
            ReportedBy = GetUserId()
        };

        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<HealthIncidentDto>.SuccessResponse(result, "Health incident reported.", 201));
    }

    /// <summary>
    /// Mark a health incident as resolved.
    /// </summary>
    [HttpPut("{id}/resolve")]
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<ActionResult<ApiResponse<HealthIncidentDto>>> Resolve(Guid id, [FromBody] ResolveHealthIncidentDto dto)
    {
        if (id != dto.Id)
        {
            return BadRequest(ApiResponse<HealthIncidentDto>.ErrorResponse("ID mismatch."));
        }

        var command = new ResolveHealthIncidentCommand
        {
            Id = dto.Id,
            ResolutionNotes = dto.ResolutionNotes,
            TreatmentDetails = dto.TreatmentDetails,
            ResolvedAt = dto.ResolvedAt,
            ResolvedBy = GetUserId()
        };

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<HealthIncidentDto>.SuccessResponse(result, "Health incident resolved."));
    }

    /// <summary>
    /// Get health history for a specific batch.
    /// </summary>
    [HttpGet("batches/{batchId}/history")] // Note: Route is structured under batches logic but controller handles incidents
    [Authorize(Roles = "Admin,Manager,Staff")]
    public async Task<ActionResult<ApiResponse<List<HealthIncidentDto>>>> GetBatchHistory(Guid batchId)
    {
        var query = new GetBatchHealthHistoryQuery { BatchId = batchId };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<List<HealthIncidentDto>>.SuccessResponse(result, "Health history retrieved."));
    }
}
