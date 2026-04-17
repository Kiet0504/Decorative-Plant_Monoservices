namespace decorativeplant_be.Infrastructure.Services;

public sealed class RoomScanSettings
{
    public const string SectionName = "RoomScan";

    /// <summary>When set, overrides AiDiagnosis:GeminiApiKey for room scan only.</summary>
    public string GeminiApiKey { get; set; } = string.Empty;

    /// <summary>When set, overrides AiDiagnosis:GeminiModel.</summary>
    public string GeminiModel { get; set; } = string.Empty;

    /// <summary>When set, overrides AiDiagnosis:GeminiBaseUrl.</summary>
    public string GeminiBaseUrl { get; set; } = string.Empty;

    public int RequestTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Catalog ranking: <c>Gemini</c> or <c>Ollama</c> (local text model; uses <c>Ollama:Model</c> or <see cref="OllamaRankModel"/>).
    /// Room photo: Gemini first when API key is set; optional <see cref="OllamaVisionModel"/> fallback.
    /// </summary>
    public string RankProvider { get; set; } = "Gemini";

    /// <summary>When set, overrides Ollama:Model for ranking only.</summary>
    public string OllamaRankModel { get; set; } = string.Empty;

    /// <summary>
    /// Local vision model for room photo JSON when Gemini fails or no Gemini key (e.g. <c>llava:7b</c>). Empty disables fallback.
    /// Requires Ollama running (<c>Ollama:BaseUrl</c>).
    /// </summary>
    public string OllamaVisionModel { get; set; } = string.Empty;
}
