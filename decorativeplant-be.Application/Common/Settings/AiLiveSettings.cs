namespace decorativeplant_be.Application.Common.Settings;

/// <summary>
/// Gemini Live API (WebSocket) — ephemeral tokens minted server-side; see <see cref="IGeminiLiveEphemeralTokenService"/>.
/// </summary>
public sealed class AiLiveSettings
{
    public const string SectionName = "AiLive";

    /// <summary>When false, <c>/ai/live/token</c> returns 503.</summary>
    public bool Enabled { get; set; }

    /// <summary>Live model id (e.g. gemini-3.1-flash-live-preview, gemini-2.0-flash-live-001).</summary>
    public string LiveModel { get; set; } = "gemini-3.1-flash-live-preview";

    /// <summary>Optional voice name for Live speech (Google Live voice id).</summary>
    public string? VoiceName { get; set; }

    /// <summary>
    /// Max number of "uses" granted to a minted ephemeral token.
    /// Too-low values can cause the Live WebSocket to open then immediately close during setup.
    /// </summary>
    public int AuthTokenUses { get; set; } = 128;

    /// <summary>
    /// Preferred REST segment for <c>POST …/auth_tokens</c> (typically <c>v1alpha</c>; discovery lists this method there).
    /// The service retries the <i>other</i> version automatically on HTTP 404.
    /// </summary>
    public string AuthTokensApiVersion { get; set; } = "v1alpha";
}
