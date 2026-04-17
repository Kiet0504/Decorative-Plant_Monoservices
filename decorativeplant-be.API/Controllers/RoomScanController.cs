using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.RoomScan;
using decorativeplant_be.Application.Features.RoomScan.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace decorativeplant_be.API.Controllers;

/// <summary>Gemini-based room corner analysis and catalog-grounded plant suggestions.</summary>
[ApiController]
[Route("api/ai")]
[Authorize]
[EnableRateLimiting("AiRoomScanPolicy")]
public class RoomScanController : BaseController
{
    [HttpPost("room-scan")]
    public async Task<ActionResult<ApiResponse<RoomScanResultDto>>> RoomScan([FromBody] RoomScanRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<RoomScanResultDto>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var result = await Mediator.Send(new RoomScanCommand
        {
            UserId = userId.Value,
            Request = request ?? new RoomScanRequestDto()
        });

        return Ok(ApiResponse<RoomScanResultDto>.SuccessResponse(result, "OK"));
    }
}
