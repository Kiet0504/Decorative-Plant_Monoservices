using System.Linq;
using decorativeplant_be.Application.Common.DTOs.AiLive;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.API.Controllers;

/// <summary>Ephemeral Gemini Live tokens for in-AR voice (client connects to Google WebSocket directly).</summary>
[Route("api/v{version:apiVersion}/ai/live")]
public class AiLiveController : BaseController
{
    private readonly IGeminiLiveEphemeralTokenService _tokens;
    private readonly ILogger<AiLiveController> _logger;

    public AiLiveController(
        IGeminiLiveEphemeralTokenService tokens,
        ILogger<AiLiveController> logger)
    {
        _tokens = tokens;
        _logger = logger;
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

    /// <summary>
    /// Client-telemetry sink: Flutter app posts Gemini Live WebSocket failures here so
    /// they surface in server logs (Android logcat is noisy and hard to read on-device).
    /// Intentionally <see cref="AllowAnonymousAttribute"/> so an expired ephemeral-token
    /// session can still report its own death; no user data is stored.
    /// </summary>
    [HttpPost("client-log")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> ClientLog(
        [FromBody] AiLiveClientLogRequestDto request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.ErrorResponse("Empty body."));
        }

        var evt = string.IsNullOrWhiteSpace(request.EventType) ? "unknown" : request.EventType.Trim();
        var reason = Truncate(request.Reason, 500);
        var message = Truncate(request.Message, 1500);
        var exType = Truncate(request.ExceptionType, 200);
        var uid = GetUserId();

        _logger.LogWarning(
            "AiLive client report [{Event}] code={Code} reason={Reason} userId={UserId} arSessionId={ArSessionId} productListingId={ProductListingId} exception={ExceptionType} message={Message}",
            evt,
            request.Code,
            reason ?? "(none)",
            uid?.ToString() ?? "(anon)",
            request.ArSessionId?.ToString() ?? "(none)",
            request.ProductListingId?.ToString() ?? "(none)",
            exType ?? "(none)",
            message ?? "(none)");

        return Ok(ApiResponse<object>.SuccessResponse(new { logged = true }, "OK"));
    }

    private static string? Truncate(string? s, int maxLen)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return s.Length <= maxLen ? s : s[..maxLen] + "…";
    }
}
