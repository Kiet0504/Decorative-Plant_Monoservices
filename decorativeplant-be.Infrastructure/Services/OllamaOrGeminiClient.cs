using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.DTOs.AiChat;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Settings;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Routes <see cref="IOllamaClient"/> calls to <see cref="OllamaClient"/> or <see cref="GeminiGenerativeContentClient"/>
/// based on <see cref="AiRoutingSettings.UseGeminiOnly"/>.
/// </summary>
public sealed class OllamaOrGeminiClient : IOllamaClient
{
    private readonly OllamaClient _ollama;
    private readonly GeminiGenerativeContentClient _gemini;
    private readonly IOptions<AiRoutingSettings> _routing;
    private readonly IOptions<OllamaSettings> _ollamaSettings;

    public OllamaOrGeminiClient(
        OllamaClient ollama,
        GeminiGenerativeContentClient gemini,
        IOptions<AiRoutingSettings> routing,
        IOptions<OllamaSettings> ollamaSettings)
    {
        _ollama = ollama;
        _gemini = gemini;
        _routing = routing;
        _ollamaSettings = ollamaSettings;
    }

    private bool UseGeminiOnly => _routing.Value.UseGeminiOnly;

    public Task<JsonDocument> ChatJsonAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default) =>
        ChatJsonAsync(systemPrompt, userPrompt, options: null, cancellationToken);

    public Task<JsonDocument> ChatJsonAsync(
        string systemPrompt,
        string userPrompt,
        OllamaJsonRequestOptions? options,
        CancellationToken cancellationToken = default) =>
        UseGeminiOnly
            ? _gemini.ChatJsonAsync(systemPrompt, userPrompt, options, cancellationToken)
            : _ollama.ChatJsonAsync(systemPrompt, userPrompt, options, cancellationToken);

    public Task<JsonDocument> ChatJsonWithImagesAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<string> imagesBase64,
        OllamaJsonRequestOptions? options,
        CancellationToken cancellationToken = default) =>
        UseGeminiOnly
            ? _gemini.ChatJsonWithImagesAsync(systemPrompt, userPrompt, imagesBase64, options, cancellationToken)
            : _ollama.ChatJsonWithImagesAsync(systemPrompt, userPrompt, imagesBase64, options, cancellationToken);

    public Task<string> ChatAsync(
        IReadOnlyList<OllamaChatTurnDto> messages,
        CancellationToken cancellationToken = default)
    {
        if (UseGeminiOnly)
        {
            var timeout = Math.Clamp(_ollamaSettings.Value.TimeoutSeconds, 5, 300);
            return _gemini.ChatPlainAsync(messages, timeout, cancellationToken);
        }

        return _ollama.ChatAsync(messages, cancellationToken);
    }
}
