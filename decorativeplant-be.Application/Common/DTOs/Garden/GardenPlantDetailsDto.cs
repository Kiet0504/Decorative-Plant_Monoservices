using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// Details object for garden plant. Maps to garden_plant.details JSONB.
/// Uses JsonPropertyName for snake_case schema compatibility.
/// </summary>
public class GardenPlantDetailsDto
{
    public string? Nickname { get; set; }

    public string? Location { get; set; }

    /// <summary>purchased|gift|propagation|manual_add</summary>
    public string? Source { get; set; }

    /// <summary>ISO date string</summary>
    [JsonPropertyName("adopted_date")]
    public string? AdoptedDate { get; set; }

    /// <summary>healthy|needs_attention|struggling</summary>
    public string? Health { get; set; }

    /// <summary>small|medium|large</summary>
    public string? Size { get; set; }

    public List<GrowthMilestoneDto>? Milestones { get; set; }
}
