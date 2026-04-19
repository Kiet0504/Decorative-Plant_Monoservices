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
