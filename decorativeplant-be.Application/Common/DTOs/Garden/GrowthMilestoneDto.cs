using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// Growth milestone for a garden plant. Stored in garden_plant.details.milestones.
/// </summary>
public class GrowthMilestoneDto
{
    public Guid Id { get; set; }

    /// <summary>first_leaf|new_growth|flowering|repotted|other</summary>
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("occurred_at")]
    public DateTime OccurredAt { get; set; }

    public string? Notes { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }
}
