using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.DTOs.AiPlacement;
using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Infrastructure.Services;

public sealed class GeminiPlacementSuggestionClient : IAiPlacementSuggestionClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly GeminiGenerativeContentClient _gemini;
    private readonly ILogger<GeminiPlacementSuggestionClient> _logger;

    public GeminiPlacementSuggestionClient(
        GeminiGenerativeContentClient gemini,
        ILogger<GeminiPlacementSuggestionClient> logger)
    {
        _gemini = gemini;
        _logger = logger;
    }

    public async Task<AiPlacementSuggestResultDto> SuggestAsync(
        AiPlacementSuggestRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var b64 = NormalizeB64(request.RoomImageBase64);
        var mime = string.IsNullOrWhiteSpace(request.RoomImageMimeType) ? "image/jpeg" : request.RoomImageMimeType.Trim();

        var systemPrompt =
            "You are an assistant that suggests where to place a single potted houseplant in a room photo. " +
            "Return JSON only. Prefer a floor corner or tabletop area with enough space and not blocking walkways. " +
            "If uncertain, still return one best-guess box with lower confidence.";

        var userPrompt =
            "Analyze this room photo and propose ONE placement region for a decorative potted plant.\n" +
            "Output JSON with shape:\n" +
            "{\n" +
            "  \"placementBoxes\": [\n" +
            "    {\n" +
            "      \"id\": \"primary\",\n" +
            "      \"label\": \"recommended_plant_area\",\n" +
            "      \"box2d\": [yMin,xMin,yMax,xMax],\n" +
            "      \"confidence\": 0.0-1.0\n" +
            "    }\n" +
            "  ]\n" +
            "}\n" +
            "Rules:\n" +
            "- box2d values are integers normalized 0..1000.\n" +
            "- Prefer an empty floor corner or a stable tabletop surface; avoid doorways/walkways.\n" +
            "- Do NOT place plants on top of beds/pillows or blocking screens.\n" +
            "- Avoid covering a person's body if visible.\n";

        using var doc = await _gemini.ChatJsonWithImagesAsync(
            systemPrompt,
            userPrompt,
            new[] { b64 },
            new OllamaJsonRequestOptions
            {
                // Use the configured Gemini model; can be overridden later with a dedicated placement model setting.
                TimeoutSeconds = 90,
                Temperature = 0.2f
            },
            cancellationToken);

        var parsed = TryParse(doc.RootElement);
        if (parsed == null || parsed.PlacementBoxes.Count == 0)
        {
            _logger.LogWarning("Placement suggestion: Gemini returned empty/invalid JSON. Falling back to center-lower box.");
            return Fallback();
        }

        // Clamp + normalize
        foreach (var b in parsed.PlacementBoxes)
        {
            if (b.Box2d == null || b.Box2d.Length != 4)
            {
                b.Box2d = new[] { 520, 360, 940, 760 };
            }
            for (var i = 0; i < 4; i++)
            {
                b.Box2d[i] = Math.Clamp(b.Box2d[i], 0, 1000);
            }
            if (string.IsNullOrWhiteSpace(b.Id)) b.Id = "primary";
            if (string.IsNullOrWhiteSpace(b.Label)) b.Label = "recommended_plant_area";
            b.Confidence = b.Confidence.HasValue ? Math.Clamp(b.Confidence.Value, 0, 1) : 0.55;
        }

        parsed.GeneratedAt = DateTime.UtcNow;
        return parsed;
    }

    private static AiPlacementSuggestResultDto? TryParse(JsonElement root)
    {
        try
        {
            // Allow either wrapped {placementBoxes:[...]} or raw array.
            if (root.ValueKind == JsonValueKind.Array)
            {
                var boxes = JsonSerializer.Deserialize<List<AiPlacementBoxDto>>(root.GetRawText(), JsonOptions) ?? new();
                return new AiPlacementSuggestResultDto { PlacementBoxes = boxes, GeneratedAt = DateTime.UtcNow };
            }

            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("placementBoxes", out var pb))
            {
                var boxes = JsonSerializer.Deserialize<List<AiPlacementBoxDto>>(pb.GetRawText(), JsonOptions) ?? new();
                return new AiPlacementSuggestResultDto { PlacementBoxes = boxes, GeneratedAt = DateTime.UtcNow };
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static AiPlacementSuggestResultDto Fallback() =>
        new()
        {
            GeneratedAt = DateTime.UtcNow,
            PlacementBoxes =
            [
                new AiPlacementBoxDto
                {
                    Id = "primary",
                    Label = "recommended_plant_area",
                    Box2d = [520, 360, 940, 760],
                    Confidence = 0.35
                }
            ]
        };

    private static string NormalizeB64(string raw)
    {
        var t = raw.Trim();
        if (t.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var comma = t.IndexOf(',', StringComparison.Ordinal);
            if (comma > 0 && comma < t.Length - 1)
            {
                t = t[(comma + 1)..].Trim();
            }
        }
        return t;
    }
}

