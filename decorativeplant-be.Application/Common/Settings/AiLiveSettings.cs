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
    /// Preferred REST segment for <c>POST …/auth_tokens</c>. Defaults to <c>v1alpha</c> —
    /// the only API surface where <c>auth_tokens</c> actually exists (v1beta returns 404).
    /// The patched <c>googleai_dart</c> in <c>packages/googleai_dart</c> correctly opens the
    /// WebSocket on <c>/v1alpha.GenerativeService.BidiGenerateContentConstrained?access_token=…</c>
    /// when a caller supplies an ephemeral token, so v1alpha is the only version that works
    /// end-to-end. The service retries the alternate version automatically on HTTP 404.
    /// </summary>
    public string AuthTokensApiVersion { get; set; } = "v1alpha";
}
