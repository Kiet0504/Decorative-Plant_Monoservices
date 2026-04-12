using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

public sealed class OllamaClient : IOllamaClient
{
    private readonly OllamaSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OllamaClient> _logger;

    public OllamaClient(
        IOptions<OllamaSettings> settings,
        IHttpClientFactory httpClientFactory,
        ILogger<OllamaClient> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<JsonDocument> ChatJsonAsync(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = string.IsNullOrWhiteSpace(_settings.BaseUrl) ? "http://localhost:11434" : _settings.BaseUrl.Trim();
        var model = string.IsNullOrWhiteSpace(_settings.Model) ? "llama3.1:8b" : _settings.Model.Trim();

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_settings.TimeoutSeconds, 5, 300));

            var req = new OllamaChatRequest
            {
                Model = model,
                Stream = false,
                Format = "json",
                Messages = new List<OllamaMessage>
                {
                    new() { Role = "system", Content = systemPrompt },
                    new() { Role = "user", Content = userPrompt },
                }
            };

            var url = $"{baseUrl.TrimEnd('/')}/api/chat";
            using var resp = await client.PostAsJsonAsync(url, req, cancellationToken);
            resp.EnsureSuccessStatusCode();

            var body = await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: cancellationToken);
            var content = body?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                throw new InvalidOperationException("Ollama returned empty content.");
            }

            try
            {
                return JsonDocument.Parse(content);
            }
            catch (Exception parseEx)
            {
                // Some models may wrap JSON in extra whitespace/quotes. Try a last-resort trim.
                var trimmed = content.Trim();
                try
                {
                    return JsonDocument.Parse(trimmed);
                }
                catch
                {
                    _logger.LogWarning(parseEx, "Failed to parse Ollama JSON. Content starts with: {Start}", SafeStart(trimmed));
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ollama chat failed.");
            throw;
        }
    }

    private static string SafeStart(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        var max = Math.Min(200, s.Length);
        return s[..max].Replace("\n", " ").Replace("\r", " ");
    }

    private sealed class OllamaChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public bool Stream { get; set; } = false;
        public string? Format { get; set; }
        public List<OllamaMessage> Messages { get; set; } = new();
    }

    private sealed class OllamaMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OllamaChatResponse
    {
        public OllamaMessage? Message { get; set; }
    }
}

