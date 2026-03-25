namespace decorativeplant_be.Application.Common.DTOs.Garden;

public class GrowthTimelineDto
{
    public GrowthTimelineHeaderDto Header { get; set; } = new();

    public List<GrowthPhotoEntryDto> Entries { get; set; } = new();

    public string? NextCursor { get; set; }
}

public class GrowthTimelineHeaderDto
{
    public Guid GardenPlantId { get; set; }

    public string? PlantNickname { get; set; }

    public string? AdoptedDate { get; set; }

    public string? CoverImageUrl { get; set; }

    public int TotalCount { get; set; }
}

public class GrowthPhotoEntryDto
{
    public Guid CareLogId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public string? Caption { get; set; }

    public DateTime? PerformedAt { get; set; }
}

