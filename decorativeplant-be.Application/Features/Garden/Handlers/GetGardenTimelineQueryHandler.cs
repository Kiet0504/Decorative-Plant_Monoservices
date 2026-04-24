using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Garden.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Handlers;

public class GetGardenTimelineQueryHandler : IRequestHandler<GetGardenTimelineQuery, List<TimelineItemDto>>
{
    private readonly IGardenRepository _gardenRepository;

    public GetGardenTimelineQueryHandler(IGardenRepository gardenRepository)
    {
        _gardenRepository = gardenRepository;
    }

    public async Task<List<TimelineItemDto>> Handle(GetGardenTimelineQuery request, CancellationToken cancellationToken)
    {
        var plant = await _gardenRepository.GetPlantByIdAsync(request.PlantId, includeTaxonomy: false, cancellationToken);
        if (plant == null || plant.UserId != request.UserId)
        {
            throw new NotFoundException("Garden plant", request.PlantId);
        }

        var careLogs = await _gardenRepository.GetCareLogsByPlantIdAsync(request.PlantId, cancellationToken);
        var diagnoses = await _gardenRepository.GetPlantDiagnosesByPlantIdAsync(request.PlantId, cancellationToken);

        var milestones = ExtractMilestones(plant.Details);

        var items = new List<TimelineItemDto>();

        foreach (var log in careLogs)
        {
            var date = log.PerformedAt ?? DateTime.MinValue;
            if (request.From.HasValue && date < request.From.Value) continue;
            if (request.To.HasValue && date > request.To.Value) continue;

            var actionType = GetLogActionType(log.LogInfo);
            items.Add(new TimelineItemDto
            {
                Id = log.Id,
                Date = date,
                Type = "care",
                Title = actionType,
                Summary = GetLogDescription(log.LogInfo),
                Mood = GetLogMoodDisplay(log.LogInfo),
                ImageUrl = GetFirstImageUrl(log.Images),
                SourceId = log.Id,
                Metadata = null
            });
        }

        foreach (var m in milestones)
        {
            var date = m.OccurredAt;
            if (request.From.HasValue && date < request.From.Value) continue;
            if (request.To.HasValue && date > request.To.Value) continue;

            items.Add(new TimelineItemDto
            {
                Id = m.Id,
                Date = date,
                Type = "milestone",
                Title = FormatMilestoneType(m.Type),
                Summary = m.Notes,
                ImageUrl = m.ImageUrl,
                SourceId = m.Id,
                Metadata = null
            });
        }

        foreach (var d in diagnoses)
        {
            var date = d.CreatedAt ?? DateTime.MinValue;
            if (request.From.HasValue && date < request.From.Value) continue;
            if (request.To.HasValue && date > request.To.Value) continue;

            var (title, summary) = GetDiagnosisTitleAndSummary(d.AiResult);
            items.Add(new TimelineItemDto
            {
                Id = d.Id,
                Date = date,
                Type = "diagnosis",
                Title = title,
                Summary = summary,
                ImageUrl = null,
                SourceId = d.Id,
                ResolvedAtUtc = d.ResolvedAtUtc,
                Metadata = null
            });
        }

        return items
            .OrderByDescending(x => x.Date)
            .Take(request.Limit)
            .ToList();
    }

    private static List<GrowthMilestoneDto> ExtractMilestones(System.Text.Json.JsonDocument? details)
    {
        if (details == null) return new List<GrowthMilestoneDto>();
        try
        {
            var root = details.RootElement;
            if (!root.TryGetProperty("milestones", out var arr)) return new List<GrowthMilestoneDto>();

            var list = new List<GrowthMilestoneDto>();
            foreach (var el in arr.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idProp) && Guid.TryParse(idProp.GetString(), out var g) ? g : Guid.NewGuid();
                var type = el.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "other" : "other";
                var occurredAt = el.TryGetProperty("occurred_at", out var dateProp) && DateTime.TryParse(dateProp.GetString(), out var dt) ? dt : DateTime.UtcNow;
                var notes = el.TryGetProperty("notes", out var notesProp) ? notesProp.GetString() : null;
                var imageUrl = el.TryGetProperty("image_url", out var urlProp) ? urlProp.GetString() : null;

                list.Add(new GrowthMilestoneDto
                {
                    Id = id,
                    Type = type,
                    OccurredAt = occurredAt,
                    Notes = notes,
                    ImageUrl = imageUrl
                });
            }
            return list;
        }
        catch
        {
            return new List<GrowthMilestoneDto>();
        }
    }

    private static string GetLogActionType(System.Text.Json.JsonDocument? logInfo)
    {
        if (logInfo == null) return "Care";
        try
        {
            var actionType = logInfo.RootElement.TryGetProperty("action_type", out var p) ? p.GetString() : null;
            return actionType ?? "Care";
        }
        catch
        {
            return "Care";
        }
    }

    /// <summary>Body text only (description). Mood is exposed on <see cref="TimelineItemDto.Mood"/>.</summary>
    private static string? GetLogDescription(System.Text.Json.JsonDocument? logInfo)
    {
        if (logInfo == null) return null;
        try
        {
            var root = logInfo.RootElement;
            return root.TryGetProperty("description", out var p) ? p.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Display label for mood (matches diary presets where legacy slugs were used).</summary>
    private static string? GetLogMoodDisplay(System.Text.Json.JsonDocument? logInfo)
    {
        if (logInfo == null) return null;
        try
        {
            if (!logInfo.RootElement.TryGetProperty("mood", out var moodEl)) return null;
            var m = moodEl.GetString();
            if (string.IsNullOrWhiteSpace(m)) return null;
            var t = m.Trim();
            return t.ToLowerInvariant() switch
            {
                "thriving" => "Thriving",
                "okay" => "Okay",
                "concerning" => "Worried",
                _ => t
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? GetFirstImageUrl(System.Text.Json.JsonDocument? images)
    {
        if (images == null) return null;
        try
        {
            var arr = images.RootElement;
            if (arr.ValueKind != JsonValueKind.Array || arr.GetArrayLength() == 0) return null;
            var first = arr[0];
            return first.TryGetProperty("url", out var p) ? p.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string FormatMilestoneType(string type)
    {
        return type switch
        {
            "first_leaf" => "First leaf",
            "new_growth" => "New growth",
            "flowering" => "Flowering",
            "repotted" => "Repotted",
            _ => "Milestone"
        };
    }

    private static (string Title, string? Summary) GetDiagnosisTitleAndSummary(System.Text.Json.JsonDocument? aiResult)
    {
        if (aiResult == null) return ("Diagnosis", null);
        try
        {
            var disease = aiResult.RootElement.TryGetProperty("disease", out var d) ? d.GetString() : null;
            var recommendations = aiResult.RootElement.TryGetProperty("recommendations", out var r) && r.ValueKind == JsonValueKind.Array
                ? string.Join("; ", r.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)))
                : null;
            return (disease ?? "Diagnosis", recommendations);
        }
        catch
        {
            return ("Diagnosis", null);
        }
    }
}
