using System.Text.Json.Serialization;

namespace decorativeplant_be.Application.Common.DTOs.AiPlacement;

public sealed class AiPlacementSuggestRequestDto
{
    /// <summary>Raw base64 image bytes (no data: prefix).</summary>
    public string RoomImageBase64 { get; set; } = string.Empty;

    public string? RoomImageMimeType { get; set; }

    /// <summary>Optional mode hint for future expansion.</summary>
    public string? Mode { get; set; } = "plantPlacement";
}

public sealed class AiPlacementBoxDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = "placement";

    /// <summary>
    /// Normalized coords [yMin, xMin, yMax, xMax] scaled 0–1000 (Gemini convention).
    /// </summary>
    [JsonPropertyName("box2d")]
    public int[] Box2d { get; set; } = new int[4];

    public double? Confidence { get; set; }

    /// <summary>
    /// Optional segmentation mask cropped to the bounding box, base64 PNG.
    /// For Gemini segmentation: this is often a probability map 0–255.
    /// </summary>
    public string? MaskPngBase64 { get; set; }
}

public sealed class AiPlacementSuggestResultDto
{
    public List<AiPlacementBoxDto> PlacementBoxes { get; set; } = new();
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public sealed class AiPlacementPreviewRequestDto
{
    /// <summary>Raw base64 image bytes (no data: prefix).</summary>
    public string RoomImageBase64 { get; set; } = string.Empty;

    public string? RoomImageMimeType { get; set; }

    public Guid ListingId { get; set; }

    /// <summary>Normalized coords [yMin,xMin,yMax,xMax] scaled 0–1000.</summary>
    public int[] PlacementBox2d { get; set; } = new int[4];

    public string? StyleKey { get; set; }

    public string? UserNotes { get; set; }
}

public sealed class AiPlacementPreviewResultDto
{
    public string PreviewImageUrl { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

