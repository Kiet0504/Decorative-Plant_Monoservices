namespace decorativeplant_be.Application.Common.Settings;

/// <summary>
/// Global AI backend selection. When <see cref="UseGeminiOnly"/> is true, the API uses Google Gemini for all features
/// that would otherwise call Ollama (chat, JSON tasks, room-scan fallbacks, diagnosis reasoning). Requires <c>AiDiagnosis:GeminiApiKey</c>.
/// </summary>
public sealed class AiRoutingSettings
{
    public const string SectionName = "AiRouting";

    /// <summary>
    /// When true, Ollama is never called; all IOllamaClient usage and room-scan local paths use Gemini instead.
    /// </summary>
    public bool UseGeminiOnly { get; set; }
}
