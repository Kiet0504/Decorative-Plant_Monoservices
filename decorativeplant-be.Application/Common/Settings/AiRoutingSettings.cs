namespace decorativeplant_be.Application.Common.Settings;

/// <summary>
/// Global AI backend selection. When <see cref="UseGeminiOnly"/> is true, the API uses Google Gemini for all features
/// that would otherwise call Ollama (chat, JSON tasks, room-scan fallbacks, diagnosis reasoning, and JSON intent classifiers
/// such as profile-shop vs care-only). Requires <c>AiDiagnosis:GeminiApiKey</c>.
/// <para>
/// <c>Ollama:IntentClassificationModel</c> values that look like Ollama model tags (e.g. llama3.2:1b) are not used as-is on Gemini:
/// the API substitutes <c>AiDiagnosis:GeminiModel</c> (default flash). Set <c>Ollama:IntentClassificationModel</c> to a Gemini id
/// (e.g. gemini-2.0-flash) if you want a specific model for JSON intent calls.
/// </para>
/// </summary>
public sealed class AiRoutingSettings
{
    public const string SectionName = "AiRouting";

    /// <summary>
    /// When true, Ollama is never called; all <c>IOllamaClient</c> usage routes to Gemini (including intent JSON and main chat).
    /// </summary>
    public bool UseGeminiOnly { get; set; }
}
