using decorativeplant_be.Application.Common.DTOs.AiPlacement;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.AiPlacement.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/ai")]
[Authorize]
[EnableRateLimiting("AiRoomScanPolicy")]
public sealed class AiPlacementController : BaseController
{
    [HttpPost("placement/suggest")]
    public async Task<ActionResult<ApiResponse<AiPlacementSuggestResultDto>>> Suggest([FromBody] AiPlacementSuggestRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<AiPlacementSuggestResultDto>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var result = await Mediator.Send(new SuggestAiPlacementCommand
        {
            UserId = userId.Value,
            Request = request ?? new AiPlacementSuggestRequestDto()
        });

        return Ok(ApiResponse<AiPlacementSuggestResultDto>.SuccessResponse(result, "OK"));
    }

    [HttpPost("placement/preview")]
    public async Task<ActionResult<ApiResponse<AiPlacementPreviewResultDto>>> Preview([FromBody] AiPlacementPreviewRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<AiPlacementPreviewResultDto>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var result = await Mediator.Send(new GenerateAiPlacementPreviewCommand
        {
            UserId = userId.Value,
            Request = request ?? new AiPlacementPreviewRequestDto()
        });

        return Ok(ApiResponse<AiPlacementPreviewResultDto>.SuccessResponse(result, "OK"));
    }
}

