namespace decorativeplant_be.Application.Common.Settings;

/// <summary>
/// Gemini Live API (WebSocket) — ephemeral tokens minted server-side; see <see cref="IGeminiLiveEphemeralTokenService"/>.
/// </summary>
public sealed class AiLiveSettings
{
    public const string SectionName = "AiLive";

    /// <summary>When false, <c>/ai/live/token</c> returns 503.</summary>
    public bool Enabled { get; set; }

    /// <summary>Live model id (e.g. gemini-2.0-flash-live-001, gemini-3.1-flash-live-preview).</summary>
    public string LiveModel { get; set; } = "gemini-2.0-flash-live-001";

    /// <summary>Optional voice name for Live speech (Google Live voice id).</summary>
    public string? VoiceName { get; set; }

    /// <summary>
    /// REST API version segment for <c>POST …/authTokens:create</c> (e.g. <c>v1alpha</c> or <c>v1beta</c>).
    /// Ephemeral tokens are documented against <c>v1alpha</c>; use <c>v1beta</c> only if your project requires it.
    /// </summary>
    public string AuthTokensApiVersion { get; set; } = "v1alpha";
}
