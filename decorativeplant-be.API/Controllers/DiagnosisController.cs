using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Diagnosis;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Diagnosis.Commands;
using decorativeplant_be.Application.Features.Diagnosis.Queries;

namespace decorativeplant_be.API.Controllers;

/// <summary>
/// API for AI plant disease diagnosis.
/// </summary>
[ApiController]
[Route("api/diagnosis")]
[Authorize]
public class DiagnosisController : BaseController
{
    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>Submit a plant image for AI diagnosis.</summary>
    [HttpPost("submit")]
    public async Task<ActionResult<ApiResponse<PlantDiagnosisDto>>> Submit([FromBody] SubmitDiagnosisRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<PlantDiagnosisDto>.ErrorResponse("User ID is required."));
        }

        var command = new SubmitDiagnosisCommand
        {
            UserId = userId.Value,
            ImageUrl = request.ImageUrl,
            UserDescription = request.UserDescription,
            GardenPlantId = request.GardenPlantId
        };

        var result = await Mediator.Send(command);
        return StatusCode(201, ApiResponse<PlantDiagnosisDto>.SuccessResponse(result, "Diagnosis completed.", 201));
    }

    /// <summary>Submit user feedback on a diagnosis.</summary>
    [HttpPost("{id:guid}/feedback")]
    public async Task<ActionResult<ApiResponse<object>>> SubmitFeedback(Guid id, [FromBody] SubmitFeedbackRequest request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("User ID is required."));
        }

        var command = new SubmitFeedbackCommand
        {
            UserId = userId.Value,
            DiagnosisId = id,
            UserFeedback = request.UserFeedback,
            ExpertNotes = request.ExpertNotes
        };

        await Mediator.Send(command);
        return Ok(ApiResponse<object>.SuccessResponse(new { }, "Feedback submitted."));
    }

    /// <summary>Get a single diagnosis by ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PlantDiagnosisDto>>> GetDiagnosis(Guid id)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<PlantDiagnosisDto>.ErrorResponse("User ID is required."));
        }

        var query = new GetDiagnosisQuery { UserId = userId.Value, Id = id };
        var result = await Mediator.Send(query);
        if (result == null)
        {
            return NotFound(ApiResponse<PlantDiagnosisDto>.ErrorResponse("Diagnosis not found.", statusCode: 404));
        }
        return Ok(ApiResponse<PlantDiagnosisDto>.SuccessResponse(result));
    }

    /// <summary>List diagnoses for the current user.</summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<PagedResultDto<PlantDiagnosisDto>>>> ListDiagnoses(
        [FromQuery] Guid? plantId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<PagedResultDto<PlantDiagnosisDto>>.ErrorResponse("User ID is required."));
        }

        var query = new GetDiagnosesQuery
        {
            UserId = userId.Value,
            GardenPlantId = plantId,
            Page = page,
            PageSize = pageSize
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<PagedResultDto<PlantDiagnosisDto>>.SuccessResponse(result));
    }
}

/// <summary>Request body for submit diagnosis.</summary>
public class SubmitDiagnosisRequest
{
    public string ImageUrl { get; set; } = string.Empty;
    public string? UserDescription { get; set; }
    public Guid? GardenPlantId { get; set; }
}

/// <summary>Request body for submit feedback.</summary>
public class SubmitFeedbackRequest
{
    public string UserFeedback { get; set; } = string.Empty;
    public string? ExpertNotes { get; set; }
}
