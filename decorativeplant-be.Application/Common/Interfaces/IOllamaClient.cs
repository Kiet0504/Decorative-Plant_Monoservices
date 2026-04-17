using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.DTOs.AiChat;

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

    /// <summary>
    /// Same as <see cref="ChatJsonAsync(string,string,CancellationToken)"/> with per-request model/timeout (e.g. small model for classification).
    /// </summary>
    Task<JsonDocument> ChatJsonAsync(
        string systemPrompt,
        string userPrompt,
        OllamaJsonRequestOptions? options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Vision + JSON: one user turn with base64 images (no data: prefix) and <c>format: json</c> (Ollama <c>/api/chat</c>).
    /// </summary>
    Task<JsonDocument> ChatJsonWithImagesAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<string> imagesBase64,
        OllamaJsonRequestOptions? options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Multi-turn chat with plain-text assistant output (no JSON format constraint).
    /// Messages must include a system message as the first entry when required by the caller.
    /// </summary>
    Task<string> ChatAsync(
        IReadOnlyList<OllamaChatTurnDto> messages,
        CancellationToken cancellationToken = default);
}

