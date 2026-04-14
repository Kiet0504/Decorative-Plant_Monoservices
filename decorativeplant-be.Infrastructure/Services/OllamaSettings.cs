namespace decorativeplant_be.Infrastructure.Services;

public sealed class OllamaSettings
{
    public const string SectionName = "Ollama";

    /// <summary>
    /// Base URL for Ollama server. Example: http://localhost:11434
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>
    /// Model name. Example: llama3.1:8b
    /// </summary>
    public string Model { get; set; } = "llama3.1:8b";

    /// <summary>
    /// Optional request timeout in seconds (HTTP).
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>
    /// When set (e.g. llama3.2:1b, phi3:mini), image+caption intent uses this small model for JSON classification instead of keyword-only routing.
    /// Leave empty to use keyword-based intent heuristics only.
    /// </summary>
    public string IntentClassificationModel { get; set; } = string.Empty;

    /// <summary>HTTP timeout for intent classification calls (seconds). Default 12.</summary>
    public int IntentClassificationTimeoutSeconds { get; set; } = 12;
}

