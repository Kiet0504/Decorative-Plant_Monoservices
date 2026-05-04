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
            "- The region MUST be a square: (yMax - yMin) MUST equal (xMax - xMin).\n" +
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
            var fallback = Fallback();
            _logger.LogInformation(
                "Placement suggestion (fallback): {Result}",
                JsonSerializer.Serialize(fallback, JsonOptions));
            return fallback;
        }

        foreach (var b in parsed.PlacementBoxes)
        {
            EnsureBox2dClampedSquare(b);
        }

        parsed.GeneratedAt = DateTime.UtcNow;
        _logger.LogInformation(
            "Placement suggestion: {Result}",
            JsonSerializer.Serialize(parsed, JsonOptions));

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

    private static AiPlacementSuggestResultDto Fallback()
    {
        var box = new AiPlacementBoxDto
        {
            Id = "primary",
            Label = "recommended_plant_area",
            Box2d = [520, 360, 940, 760],
            Confidence = 0.35
        };
        EnsureBox2dClampedSquare(box);
        return new AiPlacementSuggestResultDto
        {
            GeneratedAt = DateTime.UtcNow,
            PlacementBoxes = new List<AiPlacementBoxDto> { box }
        };
    }

    /// <summary>
    /// Clamp model output to the image, then force an axis-aligned square (equal span on x and y in 0..1000 space).
    /// </summary>
    private static void EnsureBox2dClampedSquare(AiPlacementBoxDto b)
    {
        if (b.Box2d == null || b.Box2d.Length != 4)
        {
            b.Box2d = [520, 360, 940, 760];
        }

        for (var i = 0; i < 4; i++)
        {
            b.Box2d[i] = Math.Clamp(b.Box2d[i], 0, 1000);
        }

        b.Box2d = NormalizeBox2dToSquare(b.Box2d);

        if (string.IsNullOrWhiteSpace(b.Id)) b.Id = "primary";
        if (string.IsNullOrWhiteSpace(b.Label)) b.Label = "recommended_plant_area";
        b.Confidence = b.Confidence.HasValue ? Math.Clamp(b.Confidence.Value, 0, 1) : 0.55;
    }

    private static int[] NormalizeBox2dToSquare(int[] box)
    {
        var y0 = box[0];
        var x0 = box[1];
        var y1 = box[2];
        var x1 = box[3];
        if (y1 < y0)
        {
            (y0, y1) = (y1, y0);
        }

        if (x1 < x0)
        {
            (x0, x1) = (x1, x0);
        }

        var w = Math.Max(1, x1 - x0);
        var h = Math.Max(1, y1 - y0);
        var side = Math.Max(w, h);
        var cy = (y0 + y1) / 2.0;
        var cx = (x0 + x1) / 2.0;

        // Shrink side until a square centered at (cx, cy) fits in [0, 1000]^2.
        while (side > 1)
        {
            var y0n = (int)Math.Round(cy - side / 2.0);
            var x0n = (int)Math.Round(cx - side / 2.0);
            if (y0n >= 0 && x0n >= 0 && y0n + side <= 1000 && x0n + side <= 1000)
            {
                return [y0n, x0n, y0n + side, x0n + side];
            }

            side--;
        }

        var yf = (int)Math.Clamp(Math.Round(cy - 0.5), 0, 999);
        var xf = (int)Math.Clamp(Math.Round(cx - 0.5), 0, 999);
        return [yf, xf, yf + 1, xf + 1];
    }

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

