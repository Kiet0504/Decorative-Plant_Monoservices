using System.Security.Claims;
using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.AiChat.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

/// <summary>Personalized plant assistant chat (local Ollama or Gemini when <c>AiRouting:UseGeminiOnly</c> is true).</summary>
[ApiController]
[Route("api/ai")]
[Authorize]
public class AiChatController : BaseController
{
    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>Send conversation messages and receive an assistant reply.</summary>
    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<AiChatReplyDto>>> Chat([FromBody] AiChatRequestDto request)
    {
        var userId = GetUserId(User);
        if (userId == null)
        {
            return BadRequest(ApiResponse<AiChatReplyDto>.ErrorResponse("User ID is required."));
        }

        var command = new SendAiChatMessageCommand
        {
            UserId = userId.Value,
            Messages = request.Messages ?? new List<AiChatMessageDto>(),
            GardenPlantId = request.GardenPlantId,
            AttachedImageBase64 = request.AttachedImageBase64,
            AttachedImageMimeType = request.AttachedImageMimeType,
            RoomScanFollowUp = request.RoomScanFollowUp,
            ArSessionId = request.ArSessionId,
            ProductListingId = request.ProductListingId,
            PlacementContextJson = request.PlacementContextJson,
            UtcOffsetMinutes = request.UtcOffsetMinutes
        };

        var result = await Mediator.Send(command);
        return Ok(ApiResponse<AiChatReplyDto>.SuccessResponse(result, "OK"));
    }
}
