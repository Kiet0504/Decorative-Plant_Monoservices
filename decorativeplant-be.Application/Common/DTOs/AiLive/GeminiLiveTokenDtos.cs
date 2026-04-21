namespace decorativeplant_be.Application.Common.DTOs.AiLive;

public sealed class GeminiLiveTokenRequestDto
{
    /// <summary>AR session from <c>POST /v1/ar-preview/sessions</c>.</summary>
    public Guid ArSessionId { get; set; }

    /// <summary>Optional shop listing for product title context.</summary>
    public Guid? ProductListingId { get; set; }
}

public sealed class GeminiLiveTokenResponseDto
{
    /// <summary>Pass to Live WebSocket as <c>access_token</c> query param (not the long-lived API key).</summary>
    public string EphemeralAccessToken { get; set; } = string.Empty;

    public DateTime? ExpireTimeUtc { get; set; }

    public DateTime? NewSessionExpireTimeUtc { get; set; }

    /// <summary>Model id for <c>LiveClient.connect(model: ...)</c>.</summary>
    public string LiveModel { get; set; } = string.Empty;

    /// <summary>Live speech voice id (e.g. Puck, Achernar). From <c>AiLive:VoiceName</c>.</summary>
    public string? VoiceName { get; set; }

    /// <summary>System instruction text for <c>LiveConfig.systemInstruction</c>.</summary>
    public string SystemInstruction { get; set; } = string.Empty;
}

/// <summary>
/// Client-side telemetry uploaded by the Flutter app when the Gemini Live
/// WebSocket fails (handshake, instant close, unexpected error). Surfaces on
/// the server logs so Android logcat is not the only diagnostic channel.
/// </summary>
public sealed class AiLiveClientLogRequestDto
{
    /// <summary>Short tag, e.g. <c>ws_closed</c>, <c>connect_error</c>, <c>setup_error</c>.</summary>
    public string EventType { get; set; } = "ws_closed";

    /// <summary>WebSocket close code (RFC 6455), when known.</summary>
    public int? Code { get; set; }

    /// <summary>WebSocket close reason text from the server, when known.</summary>
    public string? Reason { get; set; }

    /// <summary>AR preview session id tied to the failed Live attempt.</summary>
    public Guid? ArSessionId { get; set; }

    /// <summary>Product listing under discussion during the failed attempt.</summary>
    public Guid? ProductListingId { get; set; }

    /// <summary>Free-form message / Dart exception toString (will be truncated).</summary>
    public string? Message { get; set; }

    /// <summary>Dart runtime type of the error, when relevant.</summary>
    public string? ExceptionType { get; set; }
}
