using decorativeplant_be.Application.Common.AiChat;
using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.AiChat.Queries;
using decorativeplant_be.Application.Features.AiChat.Commands;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

/// <summary>Personalized plant assistant chat (local Ollama or Gemini when <c>AiRouting:UseGeminiOnly</c> is true).</summary>
[ApiController]
[Route("api/ai")]
[Authorize]
public class AiChatController : BaseController
{
    [HttpGet("chat/threads")]
    public async Task<ActionResult<ApiResponse<AiChatThreadListDto>>> Threads([FromQuery] int limit = 50)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<AiChatThreadListDto>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var result = await Mediator.Send(new ListAiChatThreadsQuery { UserId = userId.Value, Limit = limit });
        return Ok(ApiResponse<AiChatThreadListDto>.SuccessResponse(result, "OK"));
    }

    [HttpPost("chat/threads")]
    public async Task<ActionResult<ApiResponse<AiChatCreateThreadResultDto>>> CreateThread([FromBody] AiChatCreateThreadRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<AiChatCreateThreadResultDto>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var result = await Mediator.Send(new CreateAiChatThreadCommand { UserId = userId.Value, Title = request?.Title });
        return Ok(ApiResponse<AiChatCreateThreadResultDto>.SuccessResponse(result, "OK"));
    }

    [HttpPatch("chat/threads/{threadId:guid}")]
    public async Task<ActionResult<ApiResponse<AiChatThreadListItemDto>>> RenameThread(
        [FromRoute] Guid threadId,
        [FromBody] AiChatRenameThreadRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<AiChatThreadListItemDto>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var title = request?.Title ?? string.Empty;
        var result = await Mediator.Send(new RenameAiChatThreadCommand { UserId = userId.Value, ThreadId = threadId, Title = title });
        return Ok(ApiResponse<AiChatThreadListItemDto>.SuccessResponse(result, "OK"));
    }

    [HttpDelete("chat/threads/{threadId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteThread([FromRoute] Guid threadId)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<object>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var deleted = await Mediator.Send(new DeleteAiChatThreadCommand { UserId = userId.Value, ThreadId = threadId });
        if (!deleted)
        {
            return NotFound(ApiResponse<object>.ErrorResponse("Chat thread not found.", statusCode: 404));
        }

        return Ok(ApiResponse<object>.SuccessResponse(new { deleted = true }, "OK"));
    }

    /// <summary>Send conversation messages and receive an assistant reply.</summary>
    [HttpPost("chat")]
    public async Task<ActionResult<ApiResponse<AiChatReplyDto>>> Chat([FromBody] AiChatRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return BadRequest(ApiResponse<AiChatReplyDto>.ErrorResponse("User ID is required."));
        }

        var command = new SendAiChatMessageCommand
        {
            UserId = userId.Value,
            Messages = request.Messages ?? new List<AiChatMessageDto>(),
            IncludeUserProfileContext = request.IncludeUserProfileContext,
            IncludeGardenListContext = request.IncludeGardenListContext,
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

    [HttpGet("chat/history")]
    public async Task<ActionResult<ApiResponse<AiChatHistoryDto>>> History([FromQuery] Guid? threadId = null, [FromQuery] int limit = 200)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<AiChatHistoryDto>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var result = await Mediator.Send(new GetAiChatHistoryQuery { UserId = userId.Value, ThreadId = threadId, Limit = limit });
        return Ok(ApiResponse<AiChatHistoryDto>.SuccessResponse(result, "OK"));
    }

    [HttpPost("chat/message")]
    public async Task<ActionResult<ApiResponse<AiChatSendMessageResultDto>>> SendLatest([FromBody] AiChatSendMessageRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<AiChatSendMessageResultDto>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var result = await Mediator.Send(new SendAiChatMessageV2Command
        {
            UserId = userId.Value,
            Request = request ?? new AiChatSendMessageRequestDto()
        });

        return Ok(ApiResponse<AiChatSendMessageResultDto>.SuccessResponse(result, "OK"));
    }

    /// <summary>
    /// Generate a lightweight preview image for plant setup idea cards.
    /// Returns a curated stock preview per setup style (see <see cref="AiChatSetupPreviewImageResolver"/>), or a deterministic Picsum image when the style is unknown.
    /// </summary>
    [HttpPost("image")]
    public ActionResult<ApiResponse<AiChatGenerateImageResultDto>> GenerateImage([FromBody] AiChatGenerateImageRequestDto request)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(ApiResponse<AiChatGenerateImageResultDto>.ErrorResponse("User ID is required.", statusCode: 401));
        }

        var key = (request?.CacheKey ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            key = "setup";
        }
        if (key.Length > 120) key = key[..120];

        var prompt = (request?.Prompt ?? string.Empty).Trim();
        var url = AiChatSetupPreviewImageResolver.Resolve(key, prompt.Length > 0 ? prompt : null);

        return Ok(ApiResponse<AiChatGenerateImageResultDto>.SuccessResponse(new AiChatGenerateImageResultDto { Url = url }, "OK"));
    }
}
