using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Garden;

/// <summary>
/// Maps CareLog entity to DTOs. Handles JSONB serialization.
/// </summary>
public static class CareLogMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static CareLogDto ToDto(CareLog log)
    {
        CareLogLogInfoDto? logInfo = null;
        if (log.LogInfo != null)
        {
            logInfo = JsonSerializer.Deserialize<CareLogLogInfoDto>(log.LogInfo.RootElement.GetRawText(), JsonOptions);
        }

        List<CareLogImageDto>? images = null;
        if (log.Images != null && log.Images.RootElement.ValueKind == JsonValueKind.Array)
        {
            images = JsonSerializer.Deserialize<List<CareLogImageDto>>(log.Images.RootElement.GetRawText(), JsonOptions);
        }

        return new CareLogDto
        {
            Id = log.Id,
            GardenPlantId = log.GardenPlantId,
            ScheduleId = log.ScheduleId,
            LogInfo = logInfo,
            Images = images,
            PerformedAt = log.PerformedAt
        };
    }

    /// <summary>
    /// Builds LogInfo JsonDocument from command fields.
    /// </summary>
    public static System.Text.Json.JsonDocument? BuildLogInfoJson(string actionType, string? description, object? products, string? observations, string? mood)
    {
        var dict = new Dictionary<string, object?>
        {
            ["action_type"] = actionType
        };
        if (!string.IsNullOrEmpty(description)) dict["description"] = description;
        if (products != null) dict["products"] = products;
        if (!string.IsNullOrEmpty(observations)) dict["observations"] = observations;
        if (!string.IsNullOrEmpty(mood)) dict["mood"] = mood;

        var json = JsonSerializer.SerializeToUtf8Bytes(dict, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower });
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Builds Images JsonDocument from image list.
    /// </summary>
    public static System.Text.Json.JsonDocument? BuildImagesJson(List<CareLogImageDto>? images)
    {
        if (images == null || images.Count == 0) return null;

        var arr = images.Select(i => new Dictionary<string, object?>
        {
            ["url"] = i.Url ?? "",
            ["caption"] = i.Caption ?? "",
            ["ai_tags"] = i.AiTags ?? (object)new List<string>()
        }).ToList();

        var json = JsonSerializer.SerializeToUtf8Bytes(arr);
        return JsonDocument.Parse(json);
    }
}
