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

    public GenerateGardenPlantAiSchedulePlanQueryHandler(
        IGardenRepository gardenRepository,
        IUserAccountService userAccountService,
        IOllamaClient ollama)
    {
        _gardenRepository = gardenRepository;
        _userAccountService = userAccountService;
        _ollama = ollama;
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
- Keep tasks minimal (2-5 items). Prefer 2-3 for beginners, 3-5 for experienced users if justified by taxonomy info.
- Provide interval_days as an integer number of days between repeats (common values: 1, 2, 3, 7, 14, 30).
- Provide offset_days (0..horizonDays-1). The backend will compute next_due.
- Use user environment profile and recent care logs to avoid over-scheduling.
- If unsure, prefer "inspect weekly" and conservative watering.
- Only include fertilize/prune/repot when relevant; otherwise omit them.
""";

        var userPrompt = new StringBuilder();
        userPrompt.AppendLine($"Plant: {taxonomyName}");
        userPrompt.AppendLine();
        userPrompt.AppendLine($"Planning window: startAtUtc={startAt:O}, horizonDays={horizonDays}");
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
        userPrompt.AppendLine("Recent care logs (most recent first):");
        userPrompt.AppendLine(recentLogsJson);
        userPrompt.AppendLine();
        userPrompt.AppendLine("Taxonomy info (if present):");
        if (!string.IsNullOrWhiteSpace(taxonomyInfoJson)) userPrompt.AppendLine(taxonomyInfoJson);
        if (!string.IsNullOrWhiteSpace(careInfoJson)) userPrompt.AppendLine(careInfoJson);
        if (!string.IsNullOrWhiteSpace(growthInfoJson)) userPrompt.AppendLine(growthInfoJson);
        userPrompt.AppendLine();
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
        NormalizePlan(plan, startAt, horizonDays, user);
        plan.GeneratedAtUtc = now;
        return plan;
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

    private static void NormalizePlan(AiSchedulePlanDto plan, DateTime startAtUtc, int horizonDays, decorativeplant_be.Domain.Entities.UserAccount user)
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

        // Snap next_due time to time_of_day for consistent UI + saving/exporting.
        foreach (var t in plan.Tasks)
        {
            if (t.NextDue == null) continue;
            t.NextDue = SnapToTimeOfDayUtc(t.NextDue.Value, t.TimeOfDay);
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

        // 1) Watering capacity: ensure watering interval isn't more frequent than user can handle.
        var wateringCapacity = (user.WateringFrequency ?? "").Trim().ToLowerInvariant();
        var minWaterInterval = wateringCapacity switch
        {
            "daily" => 1,
            "every_2_3_days" => 2,
            "weekly" => 7,
            "rarely" => 14,
            _ => 1
        };

        foreach (var t in plan.Tasks)
        {
            if (!string.Equals(t.Type, "water", StringComparison.OrdinalIgnoreCase)) continue;
            var interval = t.IntervalDays ?? 7;
            if (interval < minWaterInterval)
            {
                t.IntervalDays = minWaterInterval;
                t.Frequency = FrequencyFromIntervalDays(minWaterInterval);
                changed = true;
            }
        }

        if (changed)
        {
            notes.Add($"Adjusted watering cadence to match your selected watering frequency ({user.WateringFrequency ?? "unknown"}).");
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

    private static DateTime SnapToTimeOfDayUtc(DateTime utc, string? timeOfDay)
    {
        // Ensure UTC kind
        if (utc.Kind != DateTimeKind.Utc) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);

        var tod = (timeOfDay ?? "").Trim().ToLowerInvariant();
        return tod switch
        {
            "morning" => new DateTime(utc.Year, utc.Month, utc.Day, 9, 0, 0, DateTimeKind.Utc),
            "afternoon" => new DateTime(utc.Year, utc.Month, utc.Day, 13, 0, 0, DateTimeKind.Utc),
            "evening" => new DateTime(utc.Year, utc.Month, utc.Day, 18, 0, 0, DateTimeKind.Utc),
            _ => utc
        };
    }
}

