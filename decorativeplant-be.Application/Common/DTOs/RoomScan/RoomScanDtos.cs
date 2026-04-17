namespace decorativeplant_be.Application.Common.DTOs.RoomScan;

public class RoomScanRequestDto
{
    /// <summary>Raw base64 image bytes (no data: prefix).</summary>
    public string ImageBase64 { get; set; } = string.Empty;

    public string? ImageMimeType { get; set; }
    public Guid? BranchId { get; set; }
    public decimal? MaxPrice { get; set; }
    public bool PetSafeOnly { get; set; }
    /// <summary>Optional: beginner, intermediate, expert.</summary>
    public string? SkillLevel { get; set; }
}

public class RoomScanResultDto
{
    public RoomProfileDto RoomProfile { get; set; } = new();
    public List<RoomScanRecommendationDto> Recommendations { get; set; } = new();
    public string? Disclaimer { get; set; }
}

public class RoomProfileDto
{
    /// <summary>1 (very low) to 5 (very bright).</summary>
    public int LightEstimate { get; set; } = 3;

    public string IndoorOutdoor { get; set; } = "indoor";
    public string ApproxSpace { get; set; } = "medium";
    public string PlacementHint { get; set; } = "unknown";
    public List<string> StyleTags { get; set; } = new();
    public List<string> Caveats { get; set; } = new();
    public double Confidence { get; set; }

    /// <summary>Optional server hint (e.g. local Ollama vs Gemini) — not photo uncertainty; shown apart from <see cref="Caveats"/>.</summary>
    public string? AnalysisSourceHint { get; set; }

    /// <summary>Hybrid pipeline: Gemini was tried for the photo but the result came from local vision (cloud slow or failed).</summary>
    public bool UsedLocalVisionAfterCloudFailure { get; set; }
}

public class RoomScanRecommendationDto
{
    public Guid ListingId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Price { get; set; } = "0";
    public string? ImageUrl { get; set; }
    public string Reason { get; set; } = string.Empty;
}
