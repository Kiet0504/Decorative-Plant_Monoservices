using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common;
using decorativeplant_be.Application.Common.DTOs.RoomScan;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Infrastructure.Services;

/// <summary>
/// Room photo → profile: Gemini when <see cref="RoomScanPipelineMode.Hybrid"/>; <see cref="RoomScanPipelineMode.LocalOnly"/> skips Gemini unless <see cref="AiRoutingSettings.UseGeminiOnly"/>.
/// Ollama vision via <see cref="RoomScanSettings.OllamaVisionModel"/> when hybrid fails or for local-only (never when <see cref="AiRoutingSettings.UseGeminiOnly"/>).
/// Catalog ranking: <see cref="RoomScanSettings.RankProvider"/> unless the request is local-only (Ollama rank), unless <see cref="AiRoutingSettings.UseGeminiOnly"/> (always Gemini rank when a key is available).
/// </summary>
public sealed class GeminiRoomScanClient : IRoomScanGeminiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly RoomScanSettings _roomScan;
    private readonly AiDiagnosisSettings _aiDiagnosis;
    private readonly AiRoutingSettings _aiRouting;
    private readonly IOllamaClient _ollama;
    private readonly ILogger<GeminiRoomScanClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;

    public GeminiRoomScanClient(
        IOptions<RoomScanSettings> roomScan,
        IOptions<AiDiagnosisSettings> aiDiagnosis,
        IOptions<AiRoutingSettings> aiRouting,
        IOllamaClient ollama,
        ILogger<GeminiRoomScanClient> logger,
        IHttpClientFactory httpClientFactory)
    {
        _roomScan = roomScan.Value;
        _aiDiagnosis = aiDiagnosis.Value;
        _aiRouting = aiRouting.Value;
        _ollama = ollama;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
    }

    private string ApiKey =>
        !string.IsNullOrWhiteSpace(_roomScan.GeminiApiKey) ? _roomScan.GeminiApiKey.Trim() : _aiDiagnosis.GeminiApiKey.Trim();

    private string Model
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_roomScan.GeminiModel))
            {
                return _roomScan.GeminiModel.Trim();
            }

            return string.IsNullOrWhiteSpace(_aiDiagnosis.GeminiModel)
                ? "gemini-2.5-flash"
                : _aiDiagnosis.GeminiModel.Trim();
        }
    }

    private string BaseUrl
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_roomScan.GeminiBaseUrl))
            {
                return _roomScan.GeminiBaseUrl.Trim().TrimEnd('/');
            }

            return string.IsNullOrWhiteSpace(_aiDiagnosis.GeminiBaseUrl)
                ? "https://generativelanguage.googleapis.com"
                : _aiDiagnosis.GeminiBaseUrl.Trim().TrimEnd('/');
        }
    }

    private static bool UseOllamaForRank(string? provider) =>
        string.Equals(provider, "Ollama", StringComparison.OrdinalIgnoreCase);

    private string? OllamaVisionModelOrNull =>
        string.IsNullOrWhiteSpace(_roomScan.OllamaVisionModel) ? null : _roomScan.OllamaVisionModel.Trim();

    private bool UseGeminiOnly => _aiRouting.UseGeminiOnly;

    public async Task<RoomProfileDto?> AnalyzeRoomFromImageAsync(
        string imageBase64,
        string mimeType,
        RoomScanPipelineMode pipelineMode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(imageBase64))
        {
            return null;
        }

        if (UseGeminiOnly && string.IsNullOrWhiteSpace(ApiKey))
        {
            _logger.LogWarning(
                "Room scan: AiRouting:UseGeminiOnly is true but no Gemini API key (RoomScan or AiDiagnosis) — cannot analyze photo.");
            return null;
        }

        var b64 = StripDataUrlPrefix(imageBase64.Trim());
        var mt = string.IsNullOrWhiteSpace(mimeType) ? "image/jpeg" : mimeType.Trim();
        var hadGeminiKey = !string.IsNullOrWhiteSpace(ApiKey);
        var useGeminiForPhoto = hadGeminiKey &&
            (pipelineMode == RoomScanPipelineMode.Hybrid ||
             (UseGeminiOnly && pipelineMode == RoomScanPipelineMode.LocalOnly));
        var geminiCalledButNoProfile = false;

        if (pipelineMode == RoomScanPipelineMode.LocalOnly && !UseGeminiOnly)
        {
            _logger.LogInformation("Room scan: full local pipeline — skipping Gemini for room profile.");
        }
        else if (useGeminiForPhoto)
        {
            var prompt = BuildRoomProfilePrompt();
            var body = BuildMultimodalBody(prompt, b64, mt);
            var text = await GenerateContentRawJsonAsync(body, "room-profile", cancellationToken);
            if (!string.IsNullOrWhiteSpace(text))
            {
                var fromGemini = ParseRoomProfile(text);
                if (fromGemini != null)
                {
                    return fromGemini;
                }

                geminiCalledButNoProfile = true;
                _logger.LogWarning(
                    "Room scan: Gemini room profile JSON missing or invalid; trying Ollama vision if configured.");
            }
            else
            {
                geminiCalledButNoProfile = true;
                _logger.LogWarning("Room scan: Gemini returned no text for room profile; trying Ollama vision if configured.");
            }
        }

        if (UseGeminiOnly)
        {
            _logger.LogWarning(
                "Room scan: Gemini-only mode — no Ollama vision fallback (configure Gemini or fix room profile JSON).");
            return null;
        }

        var visionModel = OllamaVisionModelOrNull;
        if (string.IsNullOrWhiteSpace(visionModel))
        {
            if (!useGeminiForPhoto && pipelineMode == RoomScanPipelineMode.Hybrid)
            {
                _logger.LogWarning(
                    "Room scan: Gemini API key not configured and RoomScan:OllamaVisionModel is empty — cannot analyze photo.");
            }
            else if (pipelineMode == RoomScanPipelineMode.LocalOnly)
            {
                _logger.LogWarning("Room scan: full local mode requires RoomScan:OllamaVisionModel.");
            }

            return null;
        }

        try
        {
            return await AnalyzeRoomWithOllamaVisionAsync(
                b64,
                pipelineMode,
                geminiCalledButNoProfile,
                visionModel,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Room scan: Ollama vision fallback failed.");
            return null;
        }
    }

    private static string StripDataUrlPrefix(string raw)
    {
        var t = raw.Trim();
        var comma = t.IndexOf(',', StringComparison.Ordinal);
        if (t.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && comma > 0)
        {
            return t[(comma + 1)..].Trim();
        }

        return t;
    }

    private async Task<RoomProfileDto?> AnalyzeRoomWithOllamaVisionAsync(
        string imageBase64,
        RoomScanPipelineMode pipelineMode,
        bool hybridGeminiCalledButNoProfile,
        string visionModel,
        CancellationToken cancellationToken)
    {
        const string systemPrompt =
            "You analyze indoor (or patio) spaces for placing ornamental houseplants. " +
            "Output a single JSON object only — no markdown, no code fences, no explanation.";

        using var doc = await _ollama.ChatJsonWithImagesAsync(
            systemPrompt,
            BuildRoomProfilePrompt(),
            new[] { imageBase64 },
            new OllamaJsonRequestOptions
            {
                Model = visionModel,
                TimeoutSeconds = Math.Clamp(_roomScan.RequestTimeoutSeconds, 30, 300),
                Temperature = 0.15f,
                LogFailuresAsWarnings = true
            },
            cancellationToken);

        var raw = doc.RootElement.GetRawText();
        var profile = ParseRoomProfile(raw);
        if (profile == null)
        {
            _logger.LogWarning("Room scan: Ollama vision returned unparseable room profile JSON.");
            return null;
        }

        profile.AnalysisSourceHint = pipelineMode == RoomScanPipelineMode.LocalOnly
            ? "Analyzed with local Ollama (full local mode)."
            : hybridGeminiCalledButNoProfile
                ? "Analyzed with local Ollama (Gemini unavailable or failed for this photo)."
                : "Analyzed with local Ollama (no Gemini API key configured).";

        profile.UsedLocalVisionAfterCloudFailure =
            pipelineMode == RoomScanPipelineMode.Hybrid && hybridGeminiCalledButNoProfile;

        return profile;
    }

    private static string BuildRoomProfilePrompt() =>
        "You analyze indoor (or patio) spaces for placing ornamental houseplants. " +
        "Look at the photo and respond with JSON only (no markdown). " +
        "Schema: {\"lightEstimate\": integer 1-5 (1=very low/artificial only, 5=bright direct sun visible), " +
        "\"indoorOutdoor\": \"indoor\"|\"outdoor\"|\"mixed\", " +
        "\"approxSpace\": \"small\"|\"medium\"|\"large\" (usable footprint for a plant), " +
        "\"placementHint\": \"shelf\"|\"floor\"|\"hanging\"|\"table\"|\"unknown\", " +
        "\"styleTags\": string[] (short style words, max 6), " +
        "\"caveats\": string[] (photo uncertainties only — e.g. glare, window not visible; max 4; do not mention AI vendor or pipeline), " +
        "\"confidence\": number 0-1 }. " +
        "Be conservative: if unsure, lower confidence and explain in caveats.";

    public async Task<IReadOnlyList<RoomScanGeminiRankItem>?> RankListingsAsync(
        RoomProfileDto roomProfile,
        IReadOnlyList<RoomScanCatalogSnippet> snippets,
        bool petSafeOnly,
        string? skillLevel,
        int maxRecommendations,
        RoomScanPipelineMode pipelineMode,
        string? rankRefinementNotes = null,
        CancellationToken cancellationToken = default)
    {
        if (snippets.Count == 0)
        {
            return Array.Empty<RoomScanGeminiRankItem>();
        }

        var rankWithOllama =
            !UseGeminiOnly &&
            (UseOllamaForRank(_roomScan.RankProvider) || pipelineMode == RoomScanPipelineMode.LocalOnly);

        if (rankWithOllama)
        {
            if (pipelineMode == RoomScanPipelineMode.LocalOnly)
            {
                _logger.LogInformation("Room scan: full local pipeline — ranking with Ollama.");
            }

            return await RankListingsWithOllamaAsync(
                roomProfile,
                snippets,
                petSafeOnly,
                skillLevel,
                maxRecommendations,
                rankRefinementNotes,
                cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            _logger.LogWarning("Room scan: Gemini API key not configured for ranking.");
            return null;
        }

        var prompt = BuildRankUserPrompt(roomProfile, snippets, petSafeOnly, skillLevel, maxRecommendations, rankRefinementNotes);

        var requestBody = new Dictionary<string, object?>
        {
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["parts"] = new object[] { new Dictionary<string, object?> { ["text"] = prompt } }
                }
            },
            ["generationConfig"] = new Dictionary<string, object?> { ["responseMimeType"] = "application/json" }
        };

        var json = JsonSerializer.Serialize(requestBody, SerializerOptions);
        var text = await PostGenerateContentAsync(json, "rank", cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return ParseRankItems(text);
    }

    private async Task<IReadOnlyList<RoomScanGeminiRankItem>?> RankListingsWithOllamaAsync(
        RoomProfileDto roomProfile,
        IReadOnlyList<RoomScanCatalogSnippet> snippets,
        bool petSafeOnly,
        string? skillLevel,
        int maxRecommendations,
        string? rankRefinementNotes,
        CancellationToken cancellationToken)
    {
        var userPrompt = BuildRankUserPrompt(roomProfile, snippets, petSafeOnly, skillLevel, maxRecommendations, rankRefinementNotes);
        var rankModel = string.IsNullOrWhiteSpace(_roomScan.OllamaRankModel)
            ? null
            : _roomScan.OllamaRankModel.Trim();

        try
        {
            using var doc = await _ollama.ChatJsonAsync(
                "You output only valid JSON with an \"items\" array. No markdown or explanation.",
                userPrompt,
                new OllamaJsonRequestOptions
                {
                    Model = rankModel,
                    TimeoutSeconds = Math.Clamp(_roomScan.RequestTimeoutSeconds, 30, 300),
                    Temperature = 0.2f,
                    LogFailuresAsWarnings = true
                },
                cancellationToken);

            return ParseRankItemsFromRoot(doc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Room scan Ollama ranking failed.");
            return null;
        }
    }

    private string BuildRankUserPrompt(
        RoomProfileDto roomProfile,
        IReadOnlyList<RoomScanCatalogSnippet> snippets,
        bool petSafeOnly,
        string? skillLevel,
        int maxRecommendations,
        string? rankRefinementNotes)
    {
        var roomJson = JsonSerializer.Serialize(
            new
            {
                roomProfile.LightEstimate,
                roomProfile.IndoorOutdoor,
                roomProfile.ApproxSpace,
                roomProfile.PlacementHint,
                roomProfile.StyleTags,
                roomProfile.Caveats,
                roomProfile.Confidence
            },
            SerializerOptions);
        var catalogJson = JsonSerializer.Serialize(
            snippets.Select(s => new
            {
                id = s.Id.ToString(),
                title = s.Title,
                careSummary = s.CareSummary,
                tags = s.Tags
            }),
            SerializerOptions);

        var skill = string.IsNullOrWhiteSpace(skillLevel) ? "unspecified" : skillLevel.Trim();
        var refinementBlock = string.IsNullOrWhiteSpace(rankRefinementNotes)
            ? ""
            : "\n\nUSER_REFINEMENT (from chat — prioritize when choosing and in each reason):\n" +
              rankRefinementNotes.Trim() + "\n";

        return
            "You work for a plant shop. Pick the best ornamental plants from CATALOG only for the customer's space. " +
            "ROOM_PROFILE_JSON:\n" + roomJson + "\n\n" +
            "CATALOG_JSON (array of {id,title,careSummary,tags}):\n" + catalogJson +
            refinementBlock +
            "\n\n" +
            "Rules: Output JSON only (no markdown). Shape: {\"items\":[" +
            "{\"listingId\": string (must be an id from catalog), \"rank\": number (1=best), \"reason\": string (plain text, max ~280 chars, 1–2 short sentences)}]}. " +
            "For each reason: do NOT restate ROOM_PROFILE_JSON as a summary (the shopper already sees light/space/placement). " +
            "Add new information: one concrete phrase from that listing's careSummary or tags (watering cadence, humidity, mature size, pet/toxicity, repotting, seasonal note) and at most a short clause linking to the room if needed. " +
            "Avoid repeating \"medium space\", \"mixed lighting\", or paraphrasing the same facts twice. " +
            "No generic praise (\"great plant\", \"beautiful\"). " +
            $"Return at most {maxRecommendations} items. Only listingIds that appear in CATALOG. " +
            (petSafeOnly
                ? "The user needs pet-safe / non-toxic options: strongly prefer plants whose careSummary or tags suggest pet safety; avoid those clearly marked toxic to cats/dogs. "
                : "") +
            $"Skill preference: {skill}. For beginner, prefer easy/low-maintenance hints in careSummary when relevant.";
    }

    private Dictionary<string, object?> BuildMultimodalBody(string textPrompt, string base64, string mimeType) =>
        new()
        {
            ["contents"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["parts"] = new object[]
                    {
                        new Dictionary<string, object?> { ["text"] = textPrompt },
                        new Dictionary<string, object?>
                        {
                            ["inline_data"] = new Dictionary<string, object?>
                            {
                                ["mime_type"] = mimeType,
                                ["data"] = base64
                            }
                        }
                    }
                }
            },
            ["generationConfig"] = new Dictionary<string, object?> { ["responseMimeType"] = "application/json" }
        };

    private async Task<string?> GenerateContentRawJsonAsync(
        Dictionary<string, object?> requestBody,
        string contextLabel,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(requestBody, SerializerOptions);
        return await PostGenerateContentAsync(json, contextLabel, cancellationToken);
    }

    private async Task<string?> PostGenerateContentAsync(
        string jsonBody,
        string contextLabel,
        CancellationToken cancellationToken)
    {
        var url =
            $"{BaseUrl}/v1beta/models/{Uri.EscapeDataString(Model)}:generateContent?key={Uri.EscapeDataString(ApiKey)}";

        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(Math.Clamp(_roomScan.RequestTimeoutSeconds, 30, 300));
            const int maxAttempts = 4;

            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
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
                    var delayMs = (int)(400 * Math.Pow(2.5, attempt - 1));
                    _logger.LogWarning(
                        "Room scan Gemini {Context} returned {Status}; retry {Attempt}/{Max} in {DelayMs}ms",
                        contextLabel,
                        (int)statusCode,
                        attempt,
                        maxAttempts,
                        delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                    continue;
                }

                _logger.LogWarning(
                    "Room scan Gemini {Context} failed: {Status} {Body}",
                    contextLabel,
                    (int)statusCode,
                    body.Length > 400 ? body[..400] : body);
                return null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Room scan Gemini request failed ({Context})", contextLabel);
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

            return StripMarkdownCodeFences(textPart.Trim());
        }
        catch
        {
            return null;
        }
    }

    private static string StripMarkdownCodeFences(string trimmed)
    {
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

        return trimmed;
    }

    private static RoomProfileDto? ParseRoomProfile(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var el = doc.RootElement;
            var profile = new RoomProfileDto
            {
                LightEstimate = el.TryGetProperty("lightEstimate", out var le) && le.TryGetInt32(out var l)
                    ? Math.Clamp(l, 1, 5)
                    : 3,
                IndoorOutdoor = el.TryGetProperty("indoorOutdoor", out var io) ? io.GetString() ?? "indoor" : "indoor",
                ApproxSpace = el.TryGetProperty("approxSpace", out var ap) ? ap.GetString() ?? "medium" : "medium",
                PlacementHint = el.TryGetProperty("placementHint", out var ph) ? ph.GetString() ?? "unknown" : "unknown",
                Confidence = el.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var conf)
                    ? Math.Clamp(conf, 0, 1)
                    : 0.5
            };

            if (el.TryGetProperty("styleTags", out var st) && st.ValueKind == JsonValueKind.Array)
            {
                profile.StyleTags = st.EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Take(8)
                    .ToList();
            }

            if (el.TryGetProperty("caveats", out var cv) && cv.ValueKind == JsonValueKind.Array)
            {
                profile.Caveats = cv.EnumerateArray()
                    .Select(x => x.GetString() ?? "")
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Take(6)
                    .ToList();
            }

            return profile;
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<RoomScanGeminiRankItem>? ParseRankItems(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ParseRankItemsFromRoot(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<RoomScanGeminiRankItem>? ParseRankItemsFromRoot(JsonElement root)
    {
        try
        {
            if (!root.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var list = new List<RoomScanGeminiRankItem>();
            foreach (var item in items.EnumerateArray())
            {
                if (!item.TryGetProperty("listingId", out var idEl))
                {
                    continue;
                }

                var idStr = idEl.ValueKind == JsonValueKind.String
                    ? idEl.GetString()
                    : idEl.GetRawText().Trim('"');
                if (string.IsNullOrWhiteSpace(idStr) || !Guid.TryParse(idStr, out var gid))
                {
                    continue;
                }

                var rank = item.TryGetProperty("rank", out var r) && r.TryGetInt32(out var ri) ? ri : list.Count + 1;
                var reason = item.TryGetProperty("reason", out var re) ? re.GetString() ?? "" : "";
                list.Add(new RoomScanGeminiRankItem
                {
                    ListingId = gid,
                    Rank = rank,
                    Reason = reason
                });
            }

            return list;
        }
        catch
        {
            return null;
        }
    }
}
