using System.Text.Json;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Domain.Entities;

namespace decorativeplant_be.Application.Features.Garden;

/// <summary>
/// Maps GardenPlant entity to DTOs. Handles JSONB deserialization.
/// </summary>
public static class GardenPlantMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public static GardenPlantDto ToDto(GardenPlant plant)
    {
        GardenPlantDetailsDto? details = null;
        if (plant.Details != null)
        {
            details = JsonSerializer.Deserialize<GardenPlantDetailsDto>(plant.Details.RootElement.GetRawText(), JsonOptions);
        }

        TaxonomySummaryDto? taxonomy = null;
        if (plant.Taxonomy != null)
        {
            taxonomy = new TaxonomySummaryDto
            {
                Id = plant.Taxonomy.Id,
                ScientificName = plant.Taxonomy.ScientificName,
                CommonName = GetCommonName(plant.Taxonomy.CommonNames)
            };
        }

        return new GardenPlantDto
        {
            Id = plant.Id,
            UserId = plant.UserId,
            TaxonomyId = plant.TaxonomyId,
            Details = details,
            ImageUrl = plant.ImageUrl,
            IsArchived = plant.IsArchived,
            CreatedAt = plant.CreatedAt,
            Taxonomy = taxonomy
        };
    }

    private static string? GetCommonName(System.Text.Json.JsonDocument? commonNames)
    {
        if (commonNames == null) return null;
        try
        {
            var en = commonNames.RootElement.TryGetProperty("en", out var enProp) ? enProp.GetString() : null;
            var vi = commonNames.RootElement.TryGetProperty("vi", out var viProp) ? viProp.GetString() : null;
            return en ?? vi;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds Details JsonDocument from command fields. Uses snake_case for JSONB schema.
    /// </summary>
    public static System.Text.Json.JsonDocument? BuildDetailsJson(
        string? nickname,
        string? location,
        string? source,
        string? adoptedDate,
        string? health,
        string? size,
        List<GrowthMilestoneDto>? milestones = null,
        Dictionary<string, object?>? extras = null)
    {
        var dict = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(nickname)) dict["nickname"] = nickname;
        if (!string.IsNullOrEmpty(location)) dict["location"] = location;
        if (!string.IsNullOrEmpty(source)) dict["source"] = source;
        if (!string.IsNullOrEmpty(adoptedDate)) dict["adopted_date"] = adoptedDate;
        if (!string.IsNullOrEmpty(health)) dict["health"] = health;
        if (!string.IsNullOrEmpty(size)) dict["size"] = size;
        if (milestones != null && milestones.Count > 0)
        {
            dict["milestones"] = milestones.Select(m => new Dictionary<string, object?>
            {
                ["id"] = m.Id.ToString(),
                ["type"] = m.Type,
                ["occurred_at"] = m.OccurredAt.ToString("O"),
                ["notes"] = m.Notes,
                ["image_url"] = m.ImageUrl
            }).ToList();
        }

        if (extras != null && extras.Count > 0)
        {
            foreach (var kv in extras)
            {
                if (kv.Value != null)
                {
                    dict[kv.Key] = kv.Value;
                }
            }
        }

        if (dict.Count == 0) return null;

        var json = JsonSerializer.SerializeToUtf8Bytes(dict, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = false
        });
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Merges command fields into existing details, preserving milestones if not provided.
    /// </summary>
    public static System.Text.Json.JsonDocument? MergeDetailsJson(
        System.Text.Json.JsonDocument? existingDetails,
        string? nickname,
        string? location,
        string? source,
        string? adoptedDate,
        string? health,
        string? size)
    {
        GardenPlantDetailsDto? existing = null;
        if (existingDetails != null)
        {
            existing = JsonSerializer.Deserialize<GardenPlantDetailsDto>(
                existingDetails.RootElement.GetRawText(),
                JsonOptions);
        }

        var mergedNickname = nickname ?? existing?.Nickname;
        var mergedLocation = location ?? existing?.Location;
        var mergedSource = source ?? existing?.Source;
        var mergedAdoptedDate = adoptedDate ?? existing?.AdoptedDate;
        var mergedHealth = health ?? existing?.Health;
        var mergedSize = size ?? existing?.Size;
        var mergedMilestones = existing?.Milestones;

        return BuildDetailsJson(mergedNickname, mergedLocation, mergedSource, mergedAdoptedDate, mergedHealth, mergedSize, mergedMilestones);
    }

    /// <summary>
    /// Adds a milestone to the details and returns updated JsonDocument.
    /// </summary>
    public static System.Text.Json.JsonDocument AddMilestone(System.Text.Json.JsonDocument? existingDetails, GrowthMilestoneDto milestone)
    {
        var details = DeserializeDetails(existingDetails);
        details.Milestones ??= new List<GrowthMilestoneDto>();
        details.Milestones.Add(milestone);
        return SerializeDetails(details);
    }

    /// <summary>
    /// Updates a milestone in the details by Id.
    /// </summary>
    public static System.Text.Json.JsonDocument? UpdateMilestone(System.Text.Json.JsonDocument? existingDetails, Guid milestoneId, string? type, DateTime? occurredAt, string? notes, string? imageUrl)
    {
        var details = DeserializeDetails(existingDetails);
        var m = details.Milestones?.FirstOrDefault(x => x.Id == milestoneId);
        if (m == null) return existingDetails;

        if (type != null) m.Type = type;
        if (occurredAt.HasValue) m.OccurredAt = occurredAt.Value;
        if (notes != null) m.Notes = notes;
        if (imageUrl != null) m.ImageUrl = imageUrl;

        return SerializeDetails(details);
    }

    /// <summary>
    /// Removes a milestone from the details by Id.
    /// </summary>
    public static System.Text.Json.JsonDocument? RemoveMilestone(System.Text.Json.JsonDocument? existingDetails, Guid milestoneId)
    {
        var details = DeserializeDetails(existingDetails);
        if (details.Milestones == null) return existingDetails;
        details.Milestones = details.Milestones.Where(x => x.Id != milestoneId).ToList();
        return SerializeDetails(details);
    }

    public static GardenPlantDetailsDto DeserializeDetails(System.Text.Json.JsonDocument? doc)
    {
        if (doc == null) return new GardenPlantDetailsDto();
        try
        {
            return JsonSerializer.Deserialize<GardenPlantDetailsDto>(doc.RootElement.GetRawText(), JsonOptions) ?? new GardenPlantDetailsDto();
        }
        catch
        {
            return new GardenPlantDetailsDto();
        }
    }

    private static System.Text.Json.JsonDocument SerializeDetails(GardenPlantDetailsDto details)
    {
        var dict = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(details.Nickname)) dict["nickname"] = details.Nickname;
        if (!string.IsNullOrEmpty(details.Location)) dict["location"] = details.Location;
        if (!string.IsNullOrEmpty(details.Source)) dict["source"] = details.Source;
        if (!string.IsNullOrEmpty(details.AdoptedDate)) dict["adopted_date"] = details.AdoptedDate;
        if (!string.IsNullOrEmpty(details.Health)) dict["health"] = details.Health;
        if (!string.IsNullOrEmpty(details.Size)) dict["size"] = details.Size;
        if (details.Milestones != null && details.Milestones.Count > 0)
        {
            dict["milestones"] = details.Milestones.Select(m => new Dictionary<string, object?>
            {
                ["id"] = m.Id.ToString(),
                ["type"] = m.Type,
                ["occurred_at"] = m.OccurredAt.ToString("O"),
                ["notes"] = m.Notes,
                ["image_url"] = m.ImageUrl
            }).ToList();
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(dict, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, WriteIndented = false });
        return JsonDocument.Parse(json);
    }
}
