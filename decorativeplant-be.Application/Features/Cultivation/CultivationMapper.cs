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

    public static BatchCareTaskDto ToTaskDto(CultivationLog entity)
    {
        if (entity == null) return new BatchCareTaskDto { Id = Guid.Empty, ProductName = "Null Entity", Status = "Error" };

        var dueDate = ExtractValue(entity.Details, "due_date") ?? entity.PerformedAt?.ToString("yyyy-MM-dd") ?? "N/A";
        var frequency = ExtractValue(entity.Details, "frequency") ?? "Once";
        var repeatEvery = ExtractValue(entity.Details, "repeat_every") ?? "7 Days";
        var status = ExtractValue(entity.Details, "status") ?? (entity.PerformedAt.HasValue ? "Done" : "Pending");

        return new BatchCareTaskDto
        {
            Id = entity.Id,
            ProductName = entity.Batch?.Taxonomy?.ScientificName ?? ExtractValue(entity.Details, "product_name") ?? "Unknown Plant",
            Activity = entity.ActivityType ?? "General Care",
            Batch = entity.Batch?.BatchCode ?? ExtractValue(entity.Details, "batch") ?? "Unknown Batch",
            Frequency = frequency,
            Date = dueDate,
            Status = status,
            RepeatEvery = repeatEvery,
            BranchId = entity.Batch?.BranchId,
            BranchName = entity.Batch?.Branch?.Name
        };
    }

    public static BatchCareTaskDetailDto ToTaskDetailDto(CultivationLog entity)
    {
        var baseDto = ToTaskDto(entity);
        return new BatchCareTaskDetailDto
        {
            Id = baseDto.Id,
            ProductName = baseDto.ProductName,
            Activity = baseDto.Activity,
            Batch = baseDto.Batch,
            Frequency = baseDto.Frequency,
            Date = baseDto.Date,
            Status = baseDto.Status,
            RepeatEvery = baseDto.RepeatEvery,
            Description = entity.Description ?? string.Empty,
            CareRequirement = ExtractValue(entity.Details, "care_requirement") ?? string.Empty
        };
    }

    private static string? ExtractValue(JsonDocument? doc, string propertyName)
    {
        if (doc == null) return null;
        if (doc.RootElement.TryGetProperty(propertyName, out var prop))
        {
            return prop.GetString();
        }
        return null;
    }

    public static JsonDocument? BuildJson(object? data)
    {
        if (data == null) return null;
        return JsonSerializer.SerializeToDocument(data, JsonOptions);
    }
}
