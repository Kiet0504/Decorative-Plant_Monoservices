using System.Net;
using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.AiPlacement;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Gemini image editing/generation (best-effort). This relies on Gemini models that can return image parts.
/// For strict mask/bbox control, swap to Imagen inpainting later.
/// </summary>
public sealed class GeminiPlacementPreviewGenerator : IAiPlacementPreviewGenerator
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AiDiagnosisSettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMediaStorageService _media;
    private readonly ILogger<GeminiPlacementPreviewGenerator> _logger;

    public GeminiPlacementPreviewGenerator(
        IOptions<AiDiagnosisSettings> settings,
        IHttpClientFactory httpClientFactory,
        IMediaStorageService media,
        ILogger<GeminiPlacementPreviewGenerator> logger)
    {
        _settings = settings.Value;
        _httpClientFactory = httpClientFactory;
        _media = media;
        _logger = logger;
    }

    private string ApiKey => string.IsNullOrWhiteSpace(_settings.GeminiApiKey) ? "" : _settings.GeminiApiKey.Trim();
    private string BaseUrl => string.IsNullOrWhiteSpace(_settings.GeminiBaseUrl) ? "https://generativelanguage.googleapis.com" : _settings.GeminiBaseUrl.Trim().TrimEnd('/');

    // Default to a widely available vision-capable model; image output depends on account/model availability.
    private string ImageModel => string.IsNullOrWhiteSpace(_settings.GeminiImageModel)
        ? "gemini-3.1-flash-image-preview"
        : _settings.GeminiImageModel.Trim();

    public async Task<AiPlacementPreviewResultDto> GenerateAsync(
        AiPlacementPreviewRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException("Gemini API key is not configured (AiDiagnosis:GeminiApiKey).");
        }

        var b64 = StripDataUrl(request.RoomImageBase64);
        var mime = string.IsNullOrWhiteSpace(request.RoomImageMimeType) ? "image/jpeg" : request.RoomImageMimeType.Trim();
        var styleKey = string.IsNullOrWhiteSpace(request.StyleKey) ? "natural" : request.StyleKey.Trim();
        var box = request.PlacementBox2d?.Length == 4 ? request.PlacementBox2d : new[] { 520, 360, 940, 760 };

        var prompt = BuildPrompt(styleKey, box, request.UserNotes);

        // Best-effort retry: first attempt, then one stricter attempt if no image part is returned.
        var (bytes, outMime) = await TryGenerateImageAsync(prompt, b64, mime, strictImageAsk: false, cancellationToken);
        if (bytes == null || bytes.Length == 0)
        {
            (bytes, outMime) = await TryGenerateImageAsync(prompt, b64, mime, strictImageAsk: true, cancellationToken);
        }

        if (bytes == null || bytes.Length == 0)
        {
            throw new InvalidOperationException("Gemini did not return an image preview. Consider using an image-capable Gemini model or switching to Imagen inpainting for this endpoint.");
        }

        await using var stream = new MemoryStream(bytes);
        var ext = outMime == "image/jpeg" ? ".jpg" : ".png";
        var url = await _media.UploadImageAsync(stream, outMime, ext, "ai-placement", cancellationToken);
        return new AiPlacementPreviewResultDto { PreviewImageUrl = url, GeneratedAt = DateTime.UtcNow };
    }

    private async Task<(byte[]? Bytes, string Mime)> TryGenerateImageAsync(
        string basePrompt,
        string roomB64,
        string roomMime,
        bool strictImageAsk,
        CancellationToken cancellationToken)
    {
        var prompt = strictImageAsk
            ? basePrompt + "\n\nIMPORTANT: Return an IMAGE output. Do not return only text."
            : basePrompt;

        var requestBody = new Dictionary<string, object?>
        {
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["role"] = "user",
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?> { ["text"] = prompt },
                        new Dictionary<string, object?>
                        {
                            ["inline_data"] = new Dictionary<string, object?>
                            {
                                ["mime_type"] = roomMime,
                                ["data"] = roomB64
                            }
                        }
                    }
                }
            },
            // Try to request image responses when supported.
            ["generationConfig"] = new Dictionary<string, object?>
            {
                ["temperature"] = 0.7,
                ["responseModalities"] = new[] { "TEXT", "IMAGE" }
            }
        };

        var json = JsonSerializer.Serialize(requestBody, SerializerOptions);
        var url = $"{BaseUrl}/v1beta/models/{Uri.EscapeDataString(ImageModel)}:generateContent?key={Uri.EscapeDataString(ApiKey)}";

        var client = _httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(180);

        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Placement preview Gemini failed: {Status} {Body}",
                (int)response.StatusCode,
                body.Length > 400 ? body[..400] : body);

            // Common: model doesn't support IMAGE modality; treat as empty result.
            return (null, "image/png");
        }

        var extracted = TryExtractFirstImage(body);
        return extracted.HasValue ? (extracted.Value.Bytes, extracted.Value.Mime) : (null, "image/png");
    }

    private static (byte[] Bytes, string Mime)? TryExtractFirstImage(string geminiResponseJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(geminiResponseJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var cand in candidates.EnumerateArray())
            {
                if (!cand.TryGetProperty("content", out var content) ||
                    !content.TryGetProperty("parts", out var parts) ||
                    parts.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var part in parts.EnumerateArray())
                {
                    // Response may use inlineData or inline_data depending on serializer.
                    if (part.TryGetProperty("inlineData", out var inlineData) || part.TryGetProperty("inline_data", out inlineData))
                    {
                        if (inlineData.ValueKind != JsonValueKind.Object) continue;
                        var mime = inlineData.TryGetProperty("mimeType", out var mt) ? mt.GetString() :
                                   inlineData.TryGetProperty("mime_type", out var mt2) ? mt2.GetString() : null;
                        var data = inlineData.TryGetProperty("data", out var d) ? d.GetString() : null;
                        if (string.IsNullOrWhiteSpace(data)) continue;
                        var bytes = Convert.FromBase64String(data);
                        return (bytes, string.IsNullOrWhiteSpace(mime) ? "image/png" : mime!);
                    }
                }
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private static string BuildPrompt(string styleKey, int[] box2d, string? notes)
    {
        var note = string.IsNullOrWhiteSpace(notes) ? "" : "\nUser notes:\n" + notes.Trim();
        return
            "Edit the provided room photo to add ONE realistic potted houseplant.\n" +
            $"Style goal: {styleKey}.\n" +
            "Placement guidance (normalized 0..1000):\n" +
            $"box2d=[{box2d[0]},{box2d[1]},{box2d[2]},{box2d[3]}].\n" +
            "Rules:\n" +
            "- Keep the room intact; change as little as possible outside the plant area.\n" +
            "- Match perspective, lighting, and shadows.\n" +
            "- Do not add extra furniture.\n" +
            "- If the box is too small/odd, place the plant NEAR it while keeping a natural-looking placement.\n" +
            note;
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

