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
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff,fulfillment_staff")]
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
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<HealthIncidentDto>>> Resolve(Guid id, [FromBody] ResolveHealthIncidentDto dto)
    {
        if (id != dto.Id)
        {
            return BadRequest(ApiResponse<HealthIncidentDto>.ErrorResponse("ID mismatch."));
        }

        var command = new ResolveHealthIncidentCommand
        {
            Id = dto.Id,
            Status = dto.Status,
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
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<List<HealthIncidentDto>>>> GetBatchHistory(Guid batchId)
    {
        var query = new GetBatchHealthHistoryQuery { BatchId = batchId };
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<List<HealthIncidentDto>>.SuccessResponse(result, "Health history retrieved."));
    }

    /// <summary>
    /// List health incidents with pagination and filtering.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<PagedResult<HealthIncidentDto>>>> List([FromQuery] GetHealthIncidentsQuery query)
    {
        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResult<HealthIncidentDto>>.SuccessResponse(result, "Health incidents retrieved."));
    }

    /// <summary>
    /// Get details of a single health incident.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<HealthIncidentDto>>> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetHealthIncidentByIdQuery { Id = id });
        if (result == null)
        {
            return NotFound(ApiResponse<HealthIncidentDto>.ErrorResponse("Health incident not found."));
        }
        return Ok(ApiResponse<HealthIncidentDto>.SuccessResponse(result, "Health incident retrieved."));
    }

    /// <summary>
    /// Get summary stats for health incidents.
    /// </summary>
    [HttpGet("summary")]
    [Authorize(Roles = "admin,branch_manager,store_staff,cultivation_staff")]
    public async Task<ActionResult<ApiResponse<HealthSummaryDto>>> GetSummary([FromQuery] Guid? branchId)
    {
        var result = await Mediator.Send(new GetHealthSummaryQuery { BranchId = branchId });
        return Ok(ApiResponse<HealthSummaryDto>.SuccessResponse(result, "Health summary retrieved."));
    }
}
