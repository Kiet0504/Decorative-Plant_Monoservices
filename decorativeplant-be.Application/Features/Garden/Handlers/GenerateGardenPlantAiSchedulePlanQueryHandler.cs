using System.Text;
using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Queries;
using decorativeplant_be.Application.Services;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public sealed class GenerateGardenPlantAiSchedulePlanQueryHandler
    : IRequestHandler<GenerateGardenPlantAiSchedulePlanQuery, AiSchedulePlanDto>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IGardenRepository _gardenRepository;
    private readonly IUserAccountService _userAccountService;
    private readonly IOllamaClient _ollama;
    private readonly IUserContentSafetyService _contentSafety;
    private readonly IPlantAssistantScopeService _plantScope;

    public GenerateGardenPlantAiSchedulePlanQueryHandler(
        IGardenRepository gardenRepository,
        IUserAccountService userAccountService,
        IOllamaClient ollama,
        IUserContentSafetyService contentSafety,
        IPlantAssistantScopeService plantScope)
    {
        _gardenRepository = gardenRepository;
        _userAccountService = userAccountService;
        _ollama = ollama;
        _contentSafety = contentSafety;
        _plantScope = plantScope;
    }

    public async Task<AiSchedulePlanDto> Handle(GenerateGardenPlantAiSchedulePlanQuery request, CancellationToken cancellationToken)
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

        var now = DateTime.UtcNow;
        var startAt = request.StartAtUtc ?? now;
        var horizonDays = request.HorizonDays <= 0 ? 30 : Math.Clamp(request.HorizonDays, 7, 90);

        var taxonomy = plant.Taxonomy;
        var taxonomyName = taxonomy?.ScientificName ?? "this plant";
        var careInfoJson = taxonomy?.CareInfo?.RootElement.GetRawText();
        var growthInfoJson = taxonomy?.GrowthInfo?.RootElement.GetRawText();
        var taxonomyInfoJson = taxonomy?.TaxonomyInfo?.RootElement.GetRawText();

        var recentLogs = await _gardenRepository.GetRecentCareLogsByPlantIdAsync(request.PlantId, limit: 12, cancellationToken);
        var recentLogsJson = BuildRecentLogsJson(recentLogs);

        var safetyParts = new List<string?> { user.DisplayName, user.LocationCity };
        foreach (var log in recentLogs)
        {
            if (log.LogInfo == null)
            {
                continue;
            }

            try
            {
                var root = log.LogInfo.RootElement;
                if (root.TryGetProperty("observations", out var obs) && obs.ValueKind == JsonValueKind.String)
                {
                    safetyParts.Add(obs.GetString());
                }

                if (root.TryGetProperty("mood", out var mood) && mood.ValueKind == JsonValueKind.String)
                {
                    safetyParts.Add(mood.GetString());
                }
            }
            catch
            {
                // ignore malformed log JSON
            }
        }

        if (!_contentSafety.IsAllowed(safetyParts))
        {
            throw new ValidationException(_contentSafety.BlockedApiMessage);
        }

        if (!_plantScope.IsInScopeForPlainUserText(string.Join('\n', safetyParts)))
        {
            throw new ValidationException(_plantScope.OutOfScopeApiMessage);
        }

        var systemPrompt = """
You are a careful plant-care scheduler.
Return JSON only (no markdown, no extra text).
Follow this exact schema:
{
  "tasks": [
    {
      "type": "water|fertilize|prune|repot|inspect",
      "interval_days": 7,
      "time_of_day": "morning|afternoon|evening",
      "offset_days": 0
    }
  ],
  "confidence": "low|medium|high",
  "notes": string[]
}
Rules:
- Species/taxonomy care requirements are the PRIMARY source for watering and fertilizing cadence (interval_days). Home/user profile ADAPTS timing and reminders; it must not replace what the plant species needs.
- Keep tasks minimal (2-5 items). Prefer 2-3 for beginners, 3-5 for experienced users if justified by taxonomy info.
- Provide interval_days as an integer number of days between repeats (common values: 1, 2, 3, 7, 14, 30).
- Provide offset_days (0..horizonDays-1). The backend will compute next_due.
- Use recent care logs to avoid duplicating tasks the user just completed.
- If taxonomy care text says water 1-2 times per week, prefer interval_days 3-4 unless logs suggest otherwise.
- If unsure on species data, prefer "inspect weekly" and conservative watering, and set confidence to "low".
- Only include fertilize/prune/repot when relevant to the species; otherwise omit them.
- If a "CURRENT DIAGNOSIS / RECOVERY CONTEXT" section is present, add at least one inspect task due within the first 2-3 days (offset_days 0-2) to monitor the issue, and bias watering/fertilize cautiously toward recovery (do not invent extreme treatments).
""";

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine($"Plant species (scientific): {taxonomyName}");
        userPrompt.AppendLine();
        userPrompt.AppendLine($"Planning window: startAtUtc={startAt:O}, horizonDays={horizonDays}");
        userPrompt.AppendLine();
        userPrompt.AppendLine("=== SPECIES / TAXONOMY (use for watering, fertilizer, light-related task timing) ===");
        if (!string.IsNullOrWhiteSpace(taxonomyInfoJson))
        {
            userPrompt.AppendLine(taxonomyInfoJson);
        }

        if (!string.IsNullOrWhiteSpace(careInfoJson))
        {
            userPrompt.AppendLine(FormatCareInfoForPrompt(careInfoJson));
        }

        if (!string.IsNullOrWhiteSpace(growthInfoJson))
        {
            userPrompt.AppendLine(growthInfoJson);
        }

        if (string.IsNullOrWhiteSpace(taxonomyInfoJson) && string.IsNullOrWhiteSpace(careInfoJson) &&
            string.IsNullOrWhiteSpace(growthInfoJson))
        {
            userPrompt.AppendLine("(No taxonomy JSON stored — infer cautiously from plant name only.)");
        }

        userPrompt.AppendLine();
        userPrompt.AppendLine("=== HOME / USER CONTEXT (adapt reminders; do not ignore species needs above) ===");
        userPrompt.AppendLine($"- sunlightExposure: {user.SunlightExposure ?? "unknown"}");
        userPrompt.AppendLine($"- roomTemperatureRange: {user.RoomTemperatureRange ?? "unknown"}");
        userPrompt.AppendLine($"- humidityLevel: {user.HumidityLevel ?? "unknown"}");
        userPrompt.AppendLine($"- wateringFrequency (habit): {user.WateringFrequency ?? "unknown"} — note: this is the user's typical habit, not a cap on how often THIS species may need water.");
        userPrompt.AppendLine($"- placementLocation: {user.PlacementLocation ?? "unknown"}");
        userPrompt.AppendLine($"- spaceSize: {user.SpaceSize ?? "unknown"}");
        userPrompt.AppendLine($"- experienceLevel: {user.ExperienceLevel ?? "unknown"}");
        userPrompt.AppendLine($"- hasChildrenOrPets: {(user.HasChildrenOrPets.HasValue ? user.HasChildrenOrPets.Value.ToString() : "unknown")}");
        userPrompt.AppendLine();
        userPrompt.AppendLine("Recent care logs (most recent first):");
        userPrompt.AppendLine(recentLogsJson);
        userPrompt.AppendLine();
        if (!string.IsNullOrWhiteSpace(request.RecoveryDiagnosisContext))
        {
            userPrompt.AppendLine("=== CURRENT DIAGNOSIS / RECOVERY CONTEXT (supportive check-ins + gentle adjustments; still obey species taxonomy above) ===");
            userPrompt.AppendLine(request.RecoveryDiagnosisContext.Trim());
            userPrompt.AppendLine();
        }

        userPrompt.AppendLine("Generate a care schedule plan JSON for this plant.");

        JsonDocument json;
        try
        {
            json = await _ollama.ChatJsonAsync(systemPrompt, userPrompt.ToString(), cancellationToken);
        }
        catch
        {
            // Degrade gracefully.
            return new AiSchedulePlanDto
            {
                Tasks = new List<CareScheduleTaskInfoDto>
                {
                    new() { Type = "inspect", Frequency = "weekly", IntervalDays = 7, TimeOfDay = "evening", OffsetDays = 1 },
                    new() { Type = "water", Frequency = "weekly", IntervalDays = 3, TimeOfDay = "morning", OffsetDays = 2 },
                },
                Confidence = "low",
                Model = null,
                GeneratedAtUtc = now,
                Notes = new List<string> { "AI scheduler unavailable; returned conservative defaults." }
            };
        }

        var plan = ParsePlan(json.RootElement, now);
        NormalizePlan(plan, startAt, horizonDays, user, request.UtcOffsetMinutes ?? 0);
        plan.GeneratedAtUtc = now;
        return plan;
    }

    /// <summary>
    /// Makes care_info JSON easier for the model to follow (handles both compact enums and rich string sections).
    /// </summary>
    private static string FormatCareInfoForPrompt(string careInfoJson)
    {
        if (string.IsNullOrWhiteSpace(careInfoJson))
        {
            return string.Empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(careInfoJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return careInfoJson;
            }

            var sb = new StringBuilder();
            sb.AppendLine("care_info (structured):");
            foreach (var prop in root.EnumerateObject())
            {
                var key = prop.Name;
                var el = prop.Value;
                switch (el.ValueKind)
                {
                    case JsonValueKind.String:
                        sb.Append("- ").Append(key).Append(": ").AppendLine(el.GetString());
                        break;
                    case JsonValueKind.Number:
                        sb.Append("- ").Append(key).Append(": ").AppendLine(el.ToString());
                        break;
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        sb.Append("- ").Append(key).Append(": ").AppendLine(el.GetBoolean().ToString());
                        break;
                    case JsonValueKind.Object:
                        sb.Append("- ").Append(key).AppendLine(":");
                        foreach (var nested in el.EnumerateObject())
                        {
                            sb.Append("  - ").Append(nested.Name).Append(": ");
                            if (nested.Value.ValueKind == JsonValueKind.String)
                            {
                                sb.AppendLine(nested.Value.GetString());
                            }
                            else
                            {
                                sb.AppendLine(nested.Value.GetRawText());
                            }
                        }

                        break;
                    default:
                        sb.Append("- ").Append(key).Append(": ").AppendLine(el.GetRawText());
                        break;
                }
            }

            return sb.ToString();
        }
        catch
        {
            return careInfoJson;
        }
    }

    private static string BuildRecentLogsJson(IEnumerable<decorativeplant_be.Domain.Entities.CareLog> logs)
    {
        // Avoid passing large images; only include action_type + performed_at + observations/mood if present.
        var list = new List<Dictionary<string, object?>>();
        foreach (var log in logs)
        {
            var item = new Dictionary<string, object?>
            {
                ["performed_at"] = log.PerformedAt,
            };
            try
            {
                if (log.LogInfo != null)
                {
                    var obj = JsonSerializer.Deserialize<Dictionary<string, object?>>(log.LogInfo.RootElement.GetRawText(), JsonOptions);
                    if (obj != null)
                    {
                        if (obj.TryGetValue("action_type", out var action)) item["action_type"] = action;
                        if (obj.TryGetValue("observations", out var obs)) item["observations"] = obs;
                        if (obj.TryGetValue("mood", out var mood)) item["mood"] = mood;
                    }
                }
            }
            catch
            {
                // ignore parse errors
            }
            list.Add(item);
        }
        return JsonSerializer.Serialize(list);
    }

    private static AiSchedulePlanDto ParsePlan(JsonElement root, DateTime nowUtc)
    {
        var plan = new AiSchedulePlanDto { GeneratedAtUtc = nowUtc };
        if (root.ValueKind != JsonValueKind.Object) return plan;

        plan.Confidence = root.TryGetProperty("confidence", out var c) ? (c.GetString() ?? "medium") : "medium";
        if (root.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Array)
        {
            plan.Notes = notes.EnumerateArray()
                .Select(x => x.ValueKind == JsonValueKind.String ? (x.GetString() ?? "") : x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .ToList();
        }

        if (root.TryGetProperty("tasks", out var tasks) && tasks.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in tasks.EnumerateArray())
            {
                if (t.ValueKind != JsonValueKind.Object) continue;
                var type = t.TryGetProperty("type", out var ty) ? (ty.GetString() ?? "") : "";
                var freq = t.TryGetProperty("frequency", out var fr) ? (fr.GetString() ?? "") : "";
                var tod = t.TryGetProperty("time_of_day", out var td) ? (td.GetString() ?? "") : "";

                int? suggestedOffsetDays = null;
                if (t.TryGetProperty("suggested_offset_days", out var offEl))
                {
                    if (offEl.ValueKind == JsonValueKind.Number && offEl.TryGetInt32(out var offInt)) suggestedOffsetDays = offInt;
                    else if (offEl.ValueKind == JsonValueKind.String && int.TryParse(offEl.GetString(), out var offParsed)) suggestedOffsetDays = offParsed;
                }

                int? offsetDays = null;
                if (t.TryGetProperty("offset_days", out var odEl))
                {
                    if (odEl.ValueKind == JsonValueKind.Number && odEl.TryGetInt32(out var odInt)) offsetDays = odInt;
                    else if (odEl.ValueKind == JsonValueKind.String && int.TryParse(odEl.GetString(), out var odParsed)) offsetDays = odParsed;
                }

                int? intervalDays = null;
                if (t.TryGetProperty("interval_days", out var intEl))
                {
                    if (intEl.ValueKind == JsonValueKind.Number && intEl.TryGetInt32(out var intInt)) intervalDays = intInt;
                    else if (intEl.ValueKind == JsonValueKind.String && int.TryParse(intEl.GetString(), out var intParsed)) intervalDays = intParsed;
                }

                var nd = t.TryGetProperty("next_due", out var ndEl) ? (ndEl.GetString() ?? "") : "";
                DateTime? nextDue = null;
                if (DateTime.TryParse(nd, out var parsed))
                {
                    nextDue = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
                }

                if (string.IsNullOrWhiteSpace(type)) continue;
                plan.Tasks.Add(new CareScheduleTaskInfoDto
                {
                    Type = type.Trim().ToLowerInvariant(),
                    Frequency = string.IsNullOrWhiteSpace(freq) ? "weekly" : freq.Trim().ToLowerInvariant(),
                    IntervalDays = intervalDays,
                    TimeOfDay = string.IsNullOrWhiteSpace(tod) ? null : tod.Trim().ToLowerInvariant(),
                    SuggestedOffsetDays = suggestedOffsetDays,
                    OffsetDays = offsetDays,
                    NextDue = nextDue
                });
            }
        }

        return plan;
    }

    private static void NormalizePlan(
        AiSchedulePlanDto plan,
        DateTime startAtUtc,
        int horizonDays,
        decorativeplant_be.Domain.Entities.UserAccount user,
        int utcOffsetMinutes)
    {
        var windowEnd = startAtUtc.AddDays(horizonDays);
        foreach (var t in plan.Tasks)
        {
            t.Type = (t.Type ?? "water").Trim().ToLowerInvariant();
            t.Frequency = (t.Frequency ?? "weekly").Trim().ToLowerInvariant();
            t.TimeOfDay = string.IsNullOrWhiteSpace(t.TimeOfDay) ? null : t.TimeOfDay.Trim().ToLowerInvariant();

            var intervalDaysFromFrequency = t.Frequency switch
            {
                "daily" => 1,
                "every_2_3_days" => 3,
                "weekly" => 7,
                "biweekly" => 14,
                "monthly" => 30,
                "rarely" => 14,
                _ => 7
            };

            // Safety: normalize unknown frequencies to weekly (keeps task, avoids dropping it)
            if (t.Frequency is not ("daily" or "every_2_3_days" or "weekly" or "biweekly" or "monthly" or "rarely"))
            {
                t.Frequency = "weekly";
                intervalDaysFromFrequency = 7;
            }

            var intervalDays = t.IntervalDays.HasValue && t.IntervalDays.Value > 0 ? t.IntervalDays.Value : intervalDaysFromFrequency;
            t.IntervalDays = intervalDays;

            // If AI only provides interval_days, normalize frequency to match (keeps UI/validators consistent).
            if (!string.IsNullOrWhiteSpace(t.Frequency))
            {
                // if frequency was defaulted or missing, align it anyway for consistency
                t.Frequency = FrequencyFromIntervalDays(intervalDays);
            }

            var offsetDays = t.OffsetDays ?? t.SuggestedOffsetDays;

            // Authoritative date computation:
            // - Prefer offset_days / suggested_offset_days (clamped)
            // - Else accept next_due if provided (but clamp)
            // - Else fallback by interval
            var fallbackDays = Math.Min(intervalDays, Math.Max(1, horizonDays - 1));
            if (offsetDays.HasValue)
            {
                var clamped = Math.Clamp(offsetDays.Value, 0, Math.Max(0, horizonDays - 1));
                t.NextDue = startAtUtc.AddDays(clamped);
            }
            else if (t.NextDue == null)
            {
                t.NextDue = startAtUtc.AddDays(fallbackDays);
            }

            // Clamp next_due to the planning window
            if (t.NextDue < startAtUtc) t.NextDue = startAtUtc.AddDays(1);
            if (t.NextDue > windowEnd) t.NextDue = startAtUtc.AddDays(Math.Min(intervalDays, Math.Max(1, horizonDays - 1)));

            // If model outputs an overly distant date for a short frequency, pull it closer.
            var maxReasonable = startAtUtc.AddDays(Math.Min(intervalDays * 2, Math.Max(1, horizonDays - 1)));
            if (t.NextDue > maxReasonable)
            {
                t.NextDue = startAtUtc.AddDays(Math.Min(intervalDays, Math.Max(1, horizonDays - 1)));
            }
        }

        // De-dupe by type (keep earliest next_due)
        plan.Tasks = plan.Tasks
            .GroupBy(x => x.Type)
            .Select(g => g.OrderBy(x => x.NextDue ?? DateTime.MaxValue).First())
            .ToList();

        ApplyOnboardingGuardrails(plan, user, startAtUtc, horizonDays);

        // Avoid stacking multiple tasks on the same day by pushing later tasks by +1 day (within window).
        var usedDates = new HashSet<DateOnly>();
        foreach (var t in plan.Tasks.OrderBy(x => x.NextDue ?? DateTime.MaxValue))
        {
            if (t.NextDue == null) continue;
            var d = DateOnly.FromDateTime(t.NextDue.Value);
            var guard = 0;
            while (usedDates.Contains(d) && guard < horizonDays)
            {
                t.NextDue = (t.NextDue.Value).AddDays(1);
                if (t.NextDue > windowEnd) break;
                d = DateOnly.FromDateTime(t.NextDue.Value);
                guard++;
            }
            if (t.NextDue != null && t.NextDue <= windowEnd) usedDates.Add(DateOnly.FromDateTime(t.NextDue.Value));
        }

        // Snap next_due time to time_of_day in the user's local zone (utcOffsetMinutes), not raw UTC hours.
        foreach (var t in plan.Tasks)
        {
            if (t.NextDue == null) continue;
            t.NextDue = SnapToTimeOfDayUtc(t.NextDue.Value, t.TimeOfDay, utcOffsetMinutes);
        }
    }

    private static void ApplyOnboardingGuardrails(
        AiSchedulePlanDto plan,
        decorativeplant_be.Domain.Entities.UserAccount user,
        DateTime startAtUtc,
        int horizonDays)
    {
        var notes = plan.Notes ?? new List<string>();
        var changed = false;

        // 1) Watering: species-first. Only stretch intervals when the user said they water "rarely"
        //    (cannot sustain frequent checks). Do not widen watering just because profile says "weekly".
        var wateringCapacity = (user.WateringFrequency ?? "").Trim().ToLowerInvariant();

        foreach (var t in plan.Tasks)
        {
            if (!string.Equals(t.Type, "water", StringComparison.OrdinalIgnoreCase)) continue;
            var interval = t.IntervalDays ?? 7;
            if (wateringCapacity == "rarely" && interval < 14)
            {
                t.IntervalDays = 14;
                t.Frequency = FrequencyFromIntervalDays(14);
                changed = true;
            }
            else if (wateringCapacity == "weekly" && interval < 7)
            {
                notes.Add(
                    $"This species may need watering about every {interval} day(s). Your profile says weekly watering — watch soil moisture and adjust if the plant dries faster.");
            }
        }

        if (changed)
        {
            notes.Add($"Watering spaced to about every 14 days because your profile indicates you water rarely ({user.WateringFrequency}).");
        }

        // 2) Task count based on experience level.
        var exp = (user.ExperienceLevel ?? "").Trim().ToLowerInvariant();
        var maxTasks = exp switch
        {
            "beginner" => 3,
            "intermediate" => 4,
            "expert" => 5,
            _ => 3
        };

        if (plan.Tasks.Count > maxTasks)
        {
            plan.Tasks = plan.Tasks
                .OrderBy(x => x.NextDue ?? DateTime.MaxValue)
                .Take(maxTasks)
                .ToList();
            notes.Add($"Kept the plan simple based on your experience level ({user.ExperienceLevel ?? "unknown"}).");
        }

        plan.Notes = notes;
    }

    private static string FrequencyFromIntervalDays(int intervalDays)
    {
        if (intervalDays <= 1) return "daily";
        if (intervalDays <= 3) return "every_2_3_days";
        if (intervalDays <= 7) return "weekly";
        if (intervalDays <= 14) return "biweekly";
        if (intervalDays <= 30) return "monthly";
        return "rarely";
    }

    /// <summary>
    /// Sets local wall-clock hour (9/13/18) on the calendar day the user sees in their timezone, then stores UTC.
    /// <paramref name="utcOffsetMinutes"/> is minutes to add to UTC to get local time (JS: <c>-getTimezoneOffset()</c>).
    /// </summary>
    private static DateTime SnapToTimeOfDayUtc(DateTime utc, string? timeOfDay, int utcOffsetMinutes)
    {
        if (utc.Kind != DateTimeKind.Utc) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        var tod = (timeOfDay ?? "").Trim().ToLowerInvariant();
        if (tod is not ("morning" or "afternoon" or "evening"))
        {
            return utc;
        }

        var offset = TimeSpan.FromMinutes(utcOffsetMinutes);
        var utcDto = new DateTimeOffset(utc, TimeSpan.Zero);
        var local = utcDto.ToOffset(offset);
        var hour = tod switch
        {
            "morning" => 9,
            "afternoon" => 13,
            "evening" => 18,
            _ => local.Hour
        };

        var snapped = new DateTimeOffset(local.Year, local.Month, local.Day, hour, 0, 0, offset);
        return snapped.UtcDateTime;
    }
}

