using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Common.DTOs.Garden;

/// <summary>
/// Image entry in a care log. Maps to care_log.images array item.
/// </summary>
public class CareLogImageDto
{
    public string Url { get; set; } = string.Empty;

    public string? Caption { get; set; }

    [JsonPropertyName("ai_tags")]
    public List<string>? AiTags { get; set; }
}
