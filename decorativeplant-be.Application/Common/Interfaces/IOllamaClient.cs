using System.Text.Json;

namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Minimal client for calling a local Ollama server for JSON-only responses.
/// </summary>
public interface IOllamaClient
{
    /// <summary>
    /// Sends a chat request and returns the parsed JSON document.
    /// Implementations should enforce JSON-only output (no markdown).
    /// </summary>
    Task<JsonDocument> ChatJsonAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default);
}

