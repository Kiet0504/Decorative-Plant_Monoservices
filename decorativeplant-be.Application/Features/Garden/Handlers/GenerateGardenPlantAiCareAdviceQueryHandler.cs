using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.Settings;
using decorativeplant_be.Application.Features.Garden;
using decorativeplant_be.Application.Features.Garden.Queries;
using decorativeplant_be.Application.Services;
using MediatR;
using Microsoft.Extensions.Options;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public sealed class GenerateGardenPlantAiCareAdviceQueryHandler
    : IRequestHandler<GenerateGardenPlantAiCareAdviceQuery, AiCareAdviceDto>
{
    private const int SchemaVersion = 3;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGardenRepository _gardenRepository;
    private readonly IUserAccountService _userAccountService;
    private readonly IOllamaClient _ollama;
    private readonly IUnitOfWork _unitOfWork;
    private readonly AiCareAdviceSettings _settings;
    private readonly AiRoutingSettings _aiRouting;
    private readonly IUserContentSafetyService _contentSafety;
    private readonly IPlantAssistantScopeService _plantScope;

    public GenerateGardenPlantAiCareAdviceQueryHandler(
        IGardenRepository gardenRepository,
        IUserAccountService userAccountService,
        IOllamaClient ollama,
        IUnitOfWork unitOfWork,
        IOptions<AiCareAdviceSettings> settings,
        IOptions<AiRoutingSettings> aiRouting,
        IUserContentSafetyService contentSafety,
        IPlantAssistantScopeService plantScope)
    {
        _gardenRepository = gardenRepository;
        _userAccountService = userAccountService;
        _ollama = ollama;
        _unitOfWork = unitOfWork;
        _settings = settings.Value;
        _aiRouting = aiRouting.Value;
        _contentSafety = contentSafety;
        _plantScope = plantScope;
    }

    public async Task<AiCareAdviceDto> Handle(GenerateGardenPlantAiCareAdviceQuery request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: true, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var user = await _userAccountService.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
        {
            throw new ValidationException("User not found.");
        }

        var detailsDto = GardenPlantMapper.DeserializeDetails(plant.Details);
        if (!_contentSafety.IsAllowed(new[] { user.DisplayName, detailsDto.Nickname, detailsDto.Location }))
        {
            throw new ValidationException(_contentSafety.BlockedApiMessage);
        }

        var profileForScope = string.Join(
            ' ',
            new[] { user.DisplayName, detailsDto.Nickname, detailsDto.Location }.Where(s => !string.IsNullOrWhiteSpace(s)));
        if (!_plantScope.IsInScopeForPlainUserText(profileForScope))
        {
            throw new ValidationException(_plantScope.OutOfScopeApiMessage);
        }

        var now = DateTime.UtcNow;
        var contextHash = ComputeContextHash(plant, user);

        var cached = TryReadCachedAdvice(plant.Details, contextHash, now, _settings.CacheTtlHours);
        if (!request.Force && cached != null)
        {
            return cached;
        }

        var (systemPrompt, userPrompt) = BuildPrompts(plant, user);
        JsonDocument json;
        try
        {
            json = await _ollama.ChatJsonAsync(systemPrompt, userPrompt, cancellationToken);
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException ||
            ex is InvalidOperationException)
        {
            // Graceful degradation: avoid 500 if the configured AI backend is unavailable.
            return new AiCareAdviceDto
            {
                Summary = _aiRouting.UseGeminiOnly
                    ? "AI tips are temporarily unavailable. Check AiDiagnosis:GeminiApiKey and Google API connectivity."
                    : "AI tips are temporarily unavailable. Please start the local Ollama server and try again.",
                Do = new List<string>
                {
                    _aiRouting.UseGeminiOnly
                        ? "Verify AiDiagnosis:GeminiApiKey and that the API can reach generativelanguage.googleapis.com."
                        : "Start Ollama (e.g. `ollama serve`) and ensure it is reachable from the API container."
                },
                Dont = new List<string>(),
                RiskNotes = new List<string>(),
                Confidence = "low",
                GeneratedAtUtc = now
            };
        }

        var advice = ParseAdvice(json.RootElement);
        advice.GeneratedAtUtc = now;

        // Cache into garden_plant.details JSONB
        if (_settings.CacheTtlHours > 0)
        {
            plant.Details = UpsertAiCareCache(plant.Details, advice, contextHash, now);
            await _gardenRepository.UpdatePlantAsync(plant, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return advice;
    }

    private static (string System, string User) BuildPrompts(decorativeplant_be.Domain.Entities.GardenPlant plant, decorativeplant_be.Domain.Entities.UserAccount user)
    {
        var taxonomy = plant.Taxonomy;
        var taxonomyName = taxonomy?.ScientificName ?? "this plant";
        var careInfoJson = taxonomy?.CareInfo?.RootElement.GetRawText();
        var growthInfoJson = taxonomy?.GrowthInfo?.RootElement.GetRawText();
        var taxonomyInfoJson = taxonomy?.TaxonomyInfo?.RootElement.GetRawText();

        var plantStatusJson = BuildSafePlantStatusJson(plant.Details);

        var system = """
You are a careful plant-care assistant.
Return JSON only (no markdown, no extra text).
Follow this exact schema:
{
  "summary": string,
  "do": string[],
  "dont": string[],
  "riskNotes": string[],
  "enrichment": {
    "description": string | null,
    "origin": string | null,
    "growthRate": string | null,
    "careRequirements": {
      "lighting": string | null,
      "temperature": string | null,
      "humidity": string | null
    } | null
  } | null,
  "confidence": "low" | "medium" | "high"
}
Rules:
- Keep tips actionable and tailored to the user's home environment.
- Avoid medical claims. If severe issues are suspected, recommend consulting a local expert.
- If pets/children are present and toxicity is relevant, include that in riskNotes.
- If taxonomy fields are missing/unknown, fill the enrichment fields with best-effort, plausible values.
- Do NOT repeat back user-provided inputs in the summary (nickname, location, adopted/purchase date, health/size, order/batch IDs, or any internal IDs).
- Summary should focus on what needs generating: key care takeaways + plant-specific guidance inferred from taxonomy/enrichment.
""";

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine($"Plant: {taxonomyName}");
        userPrompt.AppendLine();
        userPrompt.AppendLine("User environment profile:");
        userPrompt.AppendLine($"- sunlightExposure: {user.SunlightExposure ?? "unknown"}");
        userPrompt.AppendLine($"- roomTemperatureRange: {user.RoomTemperatureRange ?? "unknown"}");
        userPrompt.AppendLine($"- humidityLevel: {user.HumidityLevel ?? "unknown"}");
        userPrompt.AppendLine($"- wateringFrequency: {user.WateringFrequency ?? "unknown"}");
        userPrompt.AppendLine($"- placementLocation: {user.PlacementLocation ?? "unknown"}");
        userPrompt.AppendLine($"- spaceSize: {user.SpaceSize ?? "unknown"}");
        userPrompt.AppendLine($"- hasChildrenOrPets: {(user.HasChildrenOrPets.HasValue ? user.HasChildrenOrPets.Value.ToString() : "unknown")}");
        userPrompt.AppendLine();
        userPrompt.AppendLine("Plant status (user tracked):");
        userPrompt.AppendLine(plantStatusJson);
        userPrompt.AppendLine();
        userPrompt.AppendLine("Taxonomy info (if present):");
        if (!string.IsNullOrWhiteSpace(taxonomyInfoJson)) userPrompt.AppendLine(taxonomyInfoJson);
        if (!string.IsNullOrWhiteSpace(careInfoJson)) userPrompt.AppendLine(careInfoJson);
        if (!string.IsNullOrWhiteSpace(growthInfoJson)) userPrompt.AppendLine(growthInfoJson);
        userPrompt.AppendLine();
        userPrompt.AppendLine("Generate a personalized care summary based on all the above.");

        return (system, userPrompt.ToString());
    }

    /// <summary>Reads the first present non-empty string property (JSONB may use snake_case).</summary>
    private static string? TryGetString(JsonObject node, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!node.TryGetPropertyValue(key, out var n) || n is null) continue;
            try
            {
                var s = n.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(s)) return s.Trim();
            }
            catch
            {
                // ignore wrong node types
            }
        }

        return null;
    }

    private static string BuildSafePlantStatusJson(JsonDocument? details)
    {
        if (details == null) return "{}";
        try
        {
            var node = JsonNode.Parse(details.RootElement.GetRawText()) as JsonObject;
            if (node == null) return "{}";

            // Only pass user-meaningful, non-identifying fields to the model (no order/listing/batch IDs).
            var adopted = TryGetString(node, "adopted_date", "adoptedDate");
            var shopTitle = TryGetString(node, "shop_product_title");

            var safe = new JsonObject
            {
                ["nickname"] = node.TryGetPropertyValue("nickname", out var nickname) ? nickname : null,
                ["location"] = node.TryGetPropertyValue("location", out var location) ? location : null,
                ["source"] = node.TryGetPropertyValue("source", out var source) ? source : null,
                ["adopted_date"] = adopted is null ? null : JsonValue.Create(adopted),
                ["health"] = node.TryGetPropertyValue("health", out var health) ? health : null,
                ["size"] = node.TryGetPropertyValue("size", out var size) ? size : null,
                ["shop_product_title"] = shopTitle is null ? null : JsonValue.Create(shopTitle),
            };

            return safe.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return "{}";
        }
    }

    private static AiCareAdviceDto ParseAdvice(JsonElement root)
    {
        var dto = new AiCareAdviceDto();
        if (root.ValueKind != JsonValueKind.Object) return dto;

        dto.Summary = root.TryGetProperty("summary", out var s) ? (s.GetString() ?? "") : "";
        dto.Confidence = root.TryGetProperty("confidence", out var c) ? (c.GetString() ?? "medium") : "medium";
        dto.Do = ReadStringArray(root, "do");
        dto.Dont = ReadStringArray(root, "dont");
        dto.RiskNotes = ReadStringArray(root, "riskNotes");

        if (root.TryGetProperty("enrichment", out var enr) && enr.ValueKind == JsonValueKind.Object)
        {
            var e = new AiPlantEnrichmentDto
            {
                Description = enr.TryGetProperty("description", out var d) ? d.GetString() : null,
                Origin = enr.TryGetProperty("origin", out var o) ? o.GetString() : null,
                GrowthRate = enr.TryGetProperty("growthRate", out var gr) ? gr.GetString() : null,
            };
            if (enr.TryGetProperty("careRequirements", out var cr) && cr.ValueKind == JsonValueKind.Object)
            {
                e.CareRequirements = new AiCareRequirementsDto
                {
                    Lighting = cr.TryGetProperty("lighting", out var l) ? l.GetString() : null,
                    Temperature = cr.TryGetProperty("temperature", out var t) ? t.GetString() : null,
                    Humidity = cr.TryGetProperty("humidity", out var h) ? h.GetString() : null,
                };
            }
            dto.Enrichment = e;
        }
        return dto;
    }

    private static List<string> ReadStringArray(JsonElement root, string prop)
    {
        if (!root.TryGetProperty(prop, out var el) || el.ValueKind != JsonValueKind.Array) return new List<string>();
        return el.EnumerateArray()
            .Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() : x.ToString())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();
    }

    private static string ComputeContextHash(decorativeplant_be.Domain.Entities.GardenPlant plant, decorativeplant_be.Domain.Entities.UserAccount user)
    {
        var sb = new StringBuilder();
        sb.Append("schemaVersion=").Append(SchemaVersion).Append('|');
        sb.Append("plantId=").Append(plant.Id).Append('|');
        sb.Append("taxonomyId=").Append(plant.TaxonomyId).Append('|');
        // Exclude cached ai_care itself from the context hash, otherwise the cache write changes the hash
        // and we will never hit the cache.
        sb.Append("details=").Append(GetDetailsWithoutAiCareJson(plant.Details)).Append('|');
        sb.Append("taxonomyCare=").Append(plant.Taxonomy?.CareInfo?.RootElement.GetRawText() ?? "{}").Append('|');
        sb.Append("taxonomyGrowth=").Append(plant.Taxonomy?.GrowthInfo?.RootElement.GetRawText() ?? "{}").Append('|');
        sb.Append("taxonomyInfo=").Append(plant.Taxonomy?.TaxonomyInfo?.RootElement.GetRawText() ?? "{}").Append('|');
        sb.Append("sun=").Append(user.SunlightExposure ?? "").Append('|');
        sb.Append("temp=").Append(user.RoomTemperatureRange ?? "").Append('|');
        sb.Append("humidity=").Append(user.HumidityLevel ?? "").Append('|');
        sb.Append("waterFreq=").Append(user.WateringFrequency ?? "").Append('|');
        sb.Append("placement=").Append(user.PlacementLocation ?? "").Append('|');
        sb.Append("space=").Append(user.SpaceSize ?? "").Append('|');
        sb.Append("kidsPets=").Append(user.HasChildrenOrPets?.ToString() ?? "");

        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string GetDetailsWithoutAiCareJson(JsonDocument? details)
    {
        if (details == null) return "{}";
        try
        {
            var node = JsonNode.Parse(details.RootElement.GetRawText()) as JsonObject;
            if (node == null) return details.RootElement.GetRawText();
            node.Remove("ai_care");
            return node.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        }
        catch
        {
            return details.RootElement.GetRawText();
        }
    }

    private static AiCareAdviceDto? TryReadCachedAdvice(JsonDocument? details, string contextHash, DateTime nowUtc, int ttlHours)
    {
        if (ttlHours <= 0) return null;
        if (details == null) return null;
        try
        {
            var root = details.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            if (!root.TryGetProperty("ai_care", out var aiCare) || aiCare.ValueKind != JsonValueKind.Object) return null;

            if (!aiCare.TryGetProperty("context_hash", out var ch) || ch.GetString() != contextHash) return null;
            if (!aiCare.TryGetProperty("generated_at_utc", out var gen) || gen.ValueKind != JsonValueKind.String) return null;

            if (!DateTime.TryParse(gen.GetString(), out var generatedAt)) return null;
            generatedAt = DateTime.SpecifyKind(generatedAt, DateTimeKind.Utc);
            if (generatedAt.AddHours(ttlHours) < nowUtc) return null;

            if (!aiCare.TryGetProperty("advice", out var adviceEl) || adviceEl.ValueKind != JsonValueKind.Object) return null;
            var advice = ParseAdvice(adviceEl);
            advice.GeneratedAtUtc = generatedAt;
            advice.Model = aiCare.TryGetProperty("model", out var m) ? m.GetString() : null;
            return advice;
        }
        catch
        {
            return null;
        }
    }

    private static JsonDocument UpsertAiCareCache(JsonDocument? details, AiCareAdviceDto advice, string contextHash, DateTime nowUtc)
    {
        JsonObject root;
        if (details == null)
        {
            root = new JsonObject();
        }
        else
        {
            var node = JsonNode.Parse(details.RootElement.GetRawText());
            root = node as JsonObject ?? new JsonObject();
        }

        var adviceNode = new JsonObject
        {
            ["summary"] = advice.Summary,
            ["do"] = new JsonArray(advice.Do.Select(x => (JsonNode?)x).ToArray()),
            ["dont"] = new JsonArray(advice.Dont.Select(x => (JsonNode?)x).ToArray()),
            ["riskNotes"] = new JsonArray(advice.RiskNotes.Select(x => (JsonNode?)x).ToArray()),
            ["confidence"] = advice.Confidence,
            ["enrichment"] = advice.Enrichment == null
                ? null
                : new JsonObject
                {
                    ["description"] = advice.Enrichment.Description,
                    ["origin"] = advice.Enrichment.Origin,
                    ["growthRate"] = advice.Enrichment.GrowthRate,
                    ["careRequirements"] = advice.Enrichment.CareRequirements == null
                        ? null
                        : new JsonObject
                        {
                            ["lighting"] = advice.Enrichment.CareRequirements.Lighting,
                            ["temperature"] = advice.Enrichment.CareRequirements.Temperature,
                            ["humidity"] = advice.Enrichment.CareRequirements.Humidity,
                        }
                }
        };

        var aiCare = new JsonObject
        {
            ["context_hash"] = contextHash,
            ["generated_at_utc"] = nowUtc.ToString("O"),
            ["model"] = advice.Model,
            ["advice"] = adviceNode
        };

        root["ai_care"] = aiCare;

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return JsonDocument.Parse(json);
    }
}

