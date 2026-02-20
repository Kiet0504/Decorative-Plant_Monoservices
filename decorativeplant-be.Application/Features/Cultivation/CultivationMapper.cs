using System.Text.Json;
using decorativeplant_be.Application.Features.Cultivation.DTOs;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Cultivation;

public static class CultivationMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public static CultivationLogDto ToDto(CultivationLog entity)
    {
        object? details = null;
        if (entity.Details != null)
        {
            try { details = JsonSerializer.Deserialize<object>(entity.Details.RootElement.GetRawText(), JsonOptions); } catch {}
        }

        return new CultivationLogDto
        {
            Id = entity.Id,
            BatchId = entity.BatchId,
            BatchCode = entity.Batch?.BatchCode,
            LocationId = entity.LocationId,
            LocationName = entity.Location?.Code ?? entity.Location?.Name, // Use Code or Name
            ActivityType = entity.ActivityType ?? "Unknown",
            Description = entity.Description,
            Details = details,
            PerformedBy = entity.PerformedBy,
            PerformedByName = entity.PerformedByUser?.DisplayName ?? entity.PerformedByUser?.Email, // Use Email instead of UserName
            PerformedAt = entity.PerformedAt
        };
    }

    public static JsonDocument? BuildJson(object? data)
    {
        if (data == null) return null;
        return JsonSerializer.SerializeToDocument(data, JsonOptions);
    }
}
