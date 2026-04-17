using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.DTOs.AiChat;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Google Generative Language API (Gemini) for text + JSON + multimodal JSON — used when <see cref="AiRoutingSettings.UseGeminiOnly"/> is true.
/// </summary>
public sealed class GeminiGenerativeContentClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AiDiagnosisSettings _settings;
    private readonly ILogger<GeminiGenerativeContentClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public GeminiGenerativeContentClient(
        IOptions<AiDiagnosisSettings> settings,
        ILogger<GeminiGenerativeContentClient> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private string ApiKey =>
        string.IsNullOrWhiteSpace(_settings.GeminiApiKey) ? "" : _settings.GeminiApiKey.Trim();

    private string DefaultModel =>
        string.IsNullOrWhiteSpace(_settings.GeminiModel) ? "gemini-2.5-flash" : _settings.GeminiModel.Trim();

    private string BaseUrl =>
        string.IsNullOrWhiteSpace(_settings.GeminiBaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : _settings.GeminiBaseUrl.Trim().TrimEnd('/');

    public Task<JsonDocument> ChatJsonAsync(
        string systemPrompt,
        string userPrompt,
        OllamaJsonRequestOptions? options,
        CancellationToken cancellationToken = default) =>
        ChatJsonAsync(systemPrompt, userPrompt, options, contextLabel: "json-chat", cancellationToken);

    private async Task<JsonDocument> ChatJsonAsync(
        string systemPrompt,
        string userPrompt,
        OllamaJsonRequestOptions? options,
        string contextLabel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured (AiDiagnosis:GeminiApiKey). Required when AiRouting:UseGeminiOnly is true.");
        }

        var model = ResolveModel(options?.Model);
        var timeoutSec = options?.TimeoutSeconds ?? 120;

        var requestBody = new Dictionary<string, object?>
        {
            ["systemInstruction"] = new Dictionary<string, object?>
            {
                ["parts"] = new object[] { new Dictionary<string, object?> { ["text"] = systemPrompt } }
            },
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["parts"] = new object[] { new Dictionary<string, object?> { ["text"] = userPrompt } }
                }
            },
            ["generationConfig"] = new Dictionary<string, object?>
            {
                ["responseMimeType"] = "application/json",
                ["temperature"] = options?.Temperature ?? 0.1f
            }
        };

        var text = await PostGenerateContentRawAsync(
            requestBody,
            model,
            timeoutSec,
            contextLabel,
            cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini returned empty JSON content.");
        }

        var trimmed = text.Trim();
        try
        {
            return JsonDocument.Parse(trimmed);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini JSON parse failed. Start: {Start}", trimmed.Length > 200 ? trimmed[..200] : trimmed);
            throw;
        }
    }

    public async Task<JsonDocument> ChatJsonWithImagesAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<string> imagesBase64,
        OllamaJsonRequestOptions? options,
        CancellationToken cancellationToken = default)
    {
        if (imagesBase64 == null || imagesBase64.Count == 0)
        {
            throw new ArgumentException("At least one image is required.", nameof(imagesBase64));
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured (AiDiagnosis:GeminiApiKey). Required when AiRouting:UseGeminiOnly is true.");
        }

        var model = ResolveModel(options?.Model);
        var timeoutSec = options?.TimeoutSeconds ?? 120;

        var parts = new List<object>
        {
            new Dictionary<string, object?> { ["text"] = userPrompt }
        };
        foreach (var raw in imagesBase64)
        {
            var b64 = StripDataUrl(raw);
            parts.Add(new Dictionary<string, object?>
            {
                ["inline_data"] = new Dictionary<string, object?>
                {
                    ["mime_type"] = "image/jpeg",
                    ["data"] = b64
                }
            });
        }

        var requestBody = new Dictionary<string, object?>
        {
            ["systemInstruction"] = new Dictionary<string, object?>
            {
                ["parts"] = new object[] { new Dictionary<string, object?> { ["text"] = systemPrompt } }
            },
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["parts"] = parts.ToArray()
                }
            },
            ["generationConfig"] = new Dictionary<string, object?>
            {
                ["responseMimeType"] = "application/json",
                ["temperature"] = options?.Temperature ?? 0.15f
            }
        };

        var text = await PostGenerateContentRawAsync(
            requestBody,
            model,
            timeoutSec,
            "json-vision",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini returned empty JSON content for vision request.");
        }

        return JsonDocument.Parse(text.Trim());
    }

    public async Task<string> ChatPlainAsync(
        IReadOnlyList<OllamaChatTurnDto> messages,
        int timeoutSeconds,
        CancellationToken cancellationToken = default)
    {
        if (messages == null || messages.Count == 0)
        {
            throw new ArgumentException("At least one message is required.", nameof(messages));
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "Gemini API key is not configured (AiDiagnosis:GeminiApiKey). Required when AiRouting:UseGeminiOnly is true.");
        }

        object? systemInstruction = null;
        var contents = new List<object>();
        var i = 0;
        if (string.Equals(messages[0].Role, "system", StringComparison.OrdinalIgnoreCase))
        {
            systemInstruction = new Dictionary<string, object?>
            {
                ["parts"] = new object[] { new Dictionary<string, object?> { ["text"] = messages[0].Content } }
            };
            i = 1;
        }

        for (; i < messages.Count; i++)
        {
            var m = messages[i];
            var role = string.Equals(m.Role, "assistant", StringComparison.OrdinalIgnoreCase)
                ? "model"
                : "user";
            var parts = new List<object>();
            if (!string.IsNullOrWhiteSpace(m.Content))
            {
                parts.Add(new Dictionary<string, object?> { ["text"] = m.Content });
            }

            if (m.ImagesBase64 is { Count: > 0 })
            {
                foreach (var img in m.ImagesBase64)
                {
                    parts.Add(new Dictionary<string, object?>
                    {
                        ["inline_data"] = new Dictionary<string, object?>
                        {
                            ["mime_type"] = "image/jpeg",
                            ["data"] = StripDataUrl(img)
                        }
                    });
                }
            }

            if (parts.Count == 0)
            {
                continue;
            }

            contents.Add(new Dictionary<string, object?>
            {
                ["role"] = role,
                ["parts"] = parts.ToArray()
            });
        }

        if (contents.Count == 0)
        {
            throw new InvalidOperationException("No user/model turns to send to Gemini.");
        }

        var requestBody = new Dictionary<string, object?>
        {
            ["contents"] = contents.ToArray(),
            ["generationConfig"] = new Dictionary<string, object?> { ["temperature"] = 0.6f }
        };

        if (systemInstruction != null)
        {
            requestBody["systemInstruction"] = systemInstruction;
        }

        var text = await PostGenerateContentRawAsync(
            requestBody,
            DefaultModel,
            timeoutSeconds,
            "plain-chat",
            cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new InvalidOperationException("Gemini returned empty text.");
        }

        return text.Trim();
    }

    private string ResolveModel(string? overrideModel)
    {
        if (!string.IsNullOrWhiteSpace(overrideModel))
        {
            var o = overrideModel.Trim();
            if (o.Contains("gemini", StringComparison.OrdinalIgnoreCase) || o.StartsWith("models/", StringComparison.Ordinal))
            {
                return o.Replace("models/", "", StringComparison.OrdinalIgnoreCase).TrimStart('/');
            }

            // Ollama model name passed from intent options — use default Gemini model
            return DefaultModel;
        }

        return DefaultModel;
    }

    private async Task<string?> PostGenerateContentRawAsync(
        Dictionary<string, object?> requestBody,
        string model,
        int timeoutSeconds,
        string contextLabel,
        CancellationToken cancellationToken)
    {
        var url =
            $"{BaseUrl}/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(ApiKey)}";
        var json = JsonSerializer.Serialize(requestBody, SerializerOptions);

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 300));
            const int maxAttempts = 3;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(url, content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return ExtractTextFromGeminiResponse(body);
                }

                var statusCode = response.StatusCode;
                var retryable = statusCode is HttpStatusCode.TooManyRequests
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.BadGateway
                    or HttpStatusCode.GatewayTimeout;

                if (retryable && attempt < maxAttempts)
                {
                    await Task.Delay((int)(400 * Math.Pow(2, attempt - 1)), cancellationToken);
                    continue;
                }

                _logger.LogWarning(
                    "Gemini {Context} failed: {Status} {Body}",
                    contextLabel,
                    (int)statusCode,
                    body.Length > 400 ? body[..400] : body);
                return null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini request failed ({Context})", contextLabel);
            return null;
        }
    }

    private static string? ExtractTextFromGeminiResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var first = candidates.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Undefined)
            {
                return null;
            }

            if (!first.TryGetProperty("content", out var contentEl) ||
                !contentEl.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var textPart = parts.EnumerateArray()
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null)
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            return string.IsNullOrWhiteSpace(textPart) ? null : StripMarkdownCodeFences(textPart.Trim());
        }
        catch
        {
            return null;
        }
    }

    private static string StripMarkdownCodeFences(string s)
    {
        var t = s.Trim();
        if (t.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            t = t[7..].TrimStart();
        }
        else if (t.StartsWith("```", StringComparison.Ordinal))
        {
            t = t[3..].TrimStart();
        }

        if (t.EndsWith("```", StringComparison.Ordinal))
        {
            t = t[..^3].TrimEnd();
        }

        return t.Trim();
    }

    private static string StripDataUrl(string raw)
    {
        var t = raw.Trim();
        var comma = t.IndexOf(',', StringComparison.Ordinal);
        if (t.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
        {
            return t[(comma + 1)..].Trim();
        }

        return t;
    }
}
