namespace decorativeplant_be.Infrastructure.Services;

public class AiDiagnosisSettings
{
    public const string SectionName = "AiDiagnosis";

    /// <summary>OpenAI (default) or GeminiOllama (Gemini vision + Ollama reasoning).</summary>
    public string Provider { get; set; } = "OpenAI";

    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gpt-4o";
    public int MaxTokens { get; set; } = 1024;

    /// <summary>Google AI Studio API key for Gemini detection (Provider=GeminiOllama).</summary>
    public string GeminiApiKey { get; set; } = string.Empty;

    /// <summary>Model id, e.g. gemini-2.5-flash</summary>
    public string GeminiModel { get; set; } = "gemini-2.5-flash";

    /// <summary>
    /// Image-capable Gemini model id for image generation/editing (placement previews).
    /// Example: gemini-3.1-flash-image-preview
    /// </summary>
    public string GeminiImageModel { get; set; } = "gemini-3.1-flash-image-preview";

    /// <summary>Base URL without trailing slash; default Google Generative Language API.</summary>
    public string GeminiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com";
}
