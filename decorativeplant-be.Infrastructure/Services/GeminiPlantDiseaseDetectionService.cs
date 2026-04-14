using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Calls Google Generative Language API (Gemini) for multimodal plant disease detection JSON only.
/// </summary>
public sealed class GeminiPlantDiseaseDetectionService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AiDiagnosisSettings _settings;
    private readonly ILogger<GeminiPlantDiseaseDetectionService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public GeminiPlantDiseaseDetectionService(
        IOptions<AiDiagnosisSettings> settings,
        ILogger<GeminiPlantDiseaseDetectionService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _settings = settings.Value;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    /// <summary>
    /// Returns null if API key missing, image fetch fails, or Gemini returns unusable output.
    /// </summary>
    public async Task<GeminiDetectionResult?> DetectAsync(
        string imageUrl,
        string? userDescription,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.GeminiApiKey))
        {
            _logger.LogWarning("GeminiApiKey not configured. Skipping Gemini detection.");
            return null;
        }

        byte[] imageBytes;
        string mimeType;
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            using var resp = await client.GetAsync(imageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            resp.EnsureSuccessStatusCode();
            imageBytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
            mimeType = resp.Content.Headers.ContentType?.MediaType
                ?? GuessMimeFromUrl(imageUrl)
                ?? "image/jpeg";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download image for Gemini diagnosis: {ImageUrl}", imageUrl);
            return null;
        }

        if (imageBytes.Length == 0)
        {
            _logger.LogWarning("Empty image bytes for {ImageUrl}", imageUrl);
            return null;
        }

        var base64 = Convert.ToBase64String(imageBytes);
        return await DetectFromBase64InternalAsync(base64, mimeType, userDescription, gardenContextText: null, imageUrl, cancellationToken);
    }

    /// <summary>Same as URL path but caller supplies raw base64 (e.g. chat attachment).</summary>
    public Task<GeminiDetectionResult?> DetectFromBase64Async(
        string base64,
        string mimeType,
        string? userDescription,
        string? gardenContextText = null,
        CancellationToken cancellationToken = default) =>
        DetectFromBase64InternalAsync(base64, mimeType, userDescription, gardenContextText, "inline-base64", cancellationToken);

    private async Task<GeminiDetectionResult?> DetectFromBase64InternalAsync(
        string base64,
        string mimeType,
        string? userDescription,
        string? gardenContextText,
        string contextLabel,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.GeminiApiKey))
        {
            _logger.LogWarning("GeminiApiKey not configured. Skipping Gemini detection.");
            return null;
        }

        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        var mt = string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType.Trim();
        var model = string.IsNullOrWhiteSpace(_settings.GeminiModel) ? "gemini-2.5-flash" : _settings.GeminiModel.Trim();
        var baseUrl = string.IsNullOrWhiteSpace(_settings.GeminiBaseUrl)
            ? "https://generativelanguage.googleapis.com"
            : _settings.GeminiBaseUrl.Trim().TrimEnd('/');

        var trimmedContext = TrimGardenContextForPrompt(gardenContextText);
        var detectionPrompt =
            "You are a plant pathology assistant. Look at the image and respond with JSON only (no markdown fences). " +
            "Schema: {\"disease\": string (disease name or \\\"Healthy\\\"), \"confidence\": number between 0 and 1, " +
            "\"symptoms\": string[] (visible signs), \"notes\": string (optional short factual notes for a care advisor)}. " +
            "In \"notes\", you may briefly relate visible signs to the app context below when it plausibly explains environment or care (e.g. watering rhythm vs symptoms). Do not invent facts not supported by the image or context. " +
            "Do not include treatment or product recommendations; detection only. " +
            "User extra description: " + (string.IsNullOrWhiteSpace(userDescription) ? "None" : userDescription) +
            (string.IsNullOrEmpty(trimmedContext)
                ? ""
                : "\n\nApp / grower context (from user's garden record; may be incomplete):\n" + trimmedContext);

        var requestBody = new Dictionary<string, object?>
        {
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?> { ["text"] = detectionPrompt },
                        new Dictionary<string, object?>
                        {
                            ["inline_data"] = new Dictionary<string, object?>
                            {
                                ["mime_type"] = mt,
                                ["data"] = base64
                            }
                        }
                    }
                }
            },
            ["generationConfig"] = new Dictionary<string, object?>
            {
                ["responseMimeType"] = "application/json"
            }
        };

        var url =
            $"{baseUrl}/v1beta/models/{Uri.EscapeDataString(model)}:generateContent?key={Uri.EscapeDataString(_settings.GeminiApiKey)}";

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(120);
            var json = JsonSerializer.Serialize(requestBody, JsonSerializerOptions);
            const int maxAttempts = 4;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var content = new StringContent(json, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(url, content, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    return ParseGeminiResponse(body);
                }

                var statusCode = response.StatusCode;
                var retryable = statusCode is HttpStatusCode.TooManyRequests
                    or HttpStatusCode.ServiceUnavailable
                    or HttpStatusCode.BadGateway
                    or HttpStatusCode.GatewayTimeout;

                if (retryable && attempt < maxAttempts)
                {
                    var delayMs = (int)(400 * Math.Pow(2.5, attempt - 1));
                    _logger.LogWarning(
                        "Gemini generateContent returned {Status} ({Context}); retry {Attempt}/{Max} in {DelayMs}ms. Body: {BodyStart}",
                        (int)statusCode,
                        contextLabel,
                        attempt,
                        maxAttempts,
                        delayMs,
                        body.Length > 280 ? body[..280] + "…" : body);
                    await Task.Delay(delayMs, cancellationToken);
                    continue;
                }

                _logger.LogWarning(
                    "Gemini generateContent failed: {Status} {Body}",
                    (int)statusCode,
                    body.Length > 500 ? body[..500] : body);
                return null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini detection request failed ({Context})", contextLabel);
            return null;
        }
    }

    private static GeminiDetectionResult? ParseGeminiResponse(string body)
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

            if (!first.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var textPart = parts.EnumerateArray()
                .Select(p => p.TryGetProperty("text", out var t) ? t.GetString() : null)
                .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s));

            if (string.IsNullOrWhiteSpace(textPart))
            {
                return null;
            }

            var trimmed = textPart.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var lines = trimmed.Split('\n').ToList();
                if (lines.Count > 0 && lines[0].StartsWith("```", StringComparison.Ordinal))
                {
                    lines.RemoveAt(0);
                }

                while (lines.Count > 0 && lines[^1].Trim().StartsWith("```", StringComparison.Ordinal))
                {
                    lines.RemoveAt(lines.Count - 1);
                }

                trimmed = string.Join('\n', lines).Trim();
            }

            using var jsonEl = JsonDocument.Parse(trimmed);
            var el = jsonEl.RootElement;
            return new GeminiDetectionResult
            {
                Disease = el.TryGetProperty("disease", out var d) ? d.GetString() ?? "Unknown" : "Unknown",
                Confidence = el.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var conf) ? conf : 0.5,
                Symptoms = el.TryGetProperty("symptoms", out var s) && s.ValueKind == JsonValueKind.Array
                    ? s.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => !string.IsNullOrEmpty(x)).ToList()
                    : new List<string>(),
                Notes = el.TryGetProperty("notes", out var n) ? n.GetString() : null
            };
        }
        catch
        {
            return null;
        }
    }

    private static string TrimGardenContextForPrompt(string? gardenContextText)
    {
        if (string.IsNullOrWhiteSpace(gardenContextText))
        {
            return string.Empty;
        }

        var s = gardenContextText.Trim();
        const int max = 12000;
        return s.Length <= max ? s : s[..max] + "\n…(truncated)";
    }

    private static string? GuessMimeFromUrl(string imageUrl)
    {
        try
        {
            var path = new Uri(imageUrl).AbsolutePath.ToLowerInvariant();
            if (path.EndsWith(".png", StringComparison.Ordinal)) return "image/png";
            if (path.EndsWith(".webp", StringComparison.Ordinal)) return "image/webp";
            if (path.EndsWith(".gif", StringComparison.Ordinal)) return "image/gif";
            if (path.EndsWith(".jpg", StringComparison.Ordinal) || path.EndsWith(".jpeg", StringComparison.Ordinal))
                return "image/jpeg";
        }
        catch
        {
            // ignore
        }

        return null;
    }
}
