using System.Linq;
using decorativeplant_be.Application.Common.DTOs.AiLive;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

/// <summary>Ephemeral Gemini Live tokens for in-AR voice (client connects to Google WebSocket directly).</summary>
[Route("api/v{version:apiVersion}/ai/live")]
public class AiLiveController : BaseController
{
    private readonly IGeminiLiveEphemeralTokenService _tokens;

    public AiLiveController(IGeminiLiveEphemeralTokenService tokens)
    {
        _tokens = tokens;
    }

    /// <summary>Mint a short-lived token and AR context for <c>googleai_dart</c> LiveClient.</summary>
    [HttpPost("token")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<GeminiLiveTokenResponseDto>>> CreateToken(
        [FromBody] GeminiLiveTokenRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return BadRequest(ApiResponse<GeminiLiveTokenResponseDto>.ErrorResponse("User ID is required."));
        }

        try
        {
            var result = await _tokens.CreateTokenAsync(
                    userId.Value,
                    request.ArSessionId,
                    request.ProductListingId,
                    cancellationToken)
                .ConfigureAwait(false);
            return Ok(ApiResponse<GeminiLiveTokenResponseDto>.SuccessResponse(result, "OK"));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ApiResponse<GeminiLiveTokenResponseDto>.ErrorResponse(ex.Errors.FirstOrDefault() ?? ex.Message));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse<GeminiLiveTokenResponseDto>.ErrorResponse(ex.Message));
        }
    }
}
