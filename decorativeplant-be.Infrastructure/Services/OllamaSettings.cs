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
}

