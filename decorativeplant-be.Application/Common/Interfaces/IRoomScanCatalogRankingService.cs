using decorativeplant_be.Application.Common.DTOs.RoomScan;

namespace decorativeplant_be.Application.Common.Interfaces;

/// <summary>
/// Loads catalog candidates, calls Gemini/Ollama ranking, and merges results — shared by room scan and chat refinement.
/// </summary>
public interface IRoomScanCatalogRankingService
{
    Task<RoomScanCatalogRankingResult> GetRecommendationsAsync(
        RoomScanCatalogRankingRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class RoomScanCatalogRankingRequest
{
    public required RoomProfileDto Profile { get; init; }

    public Guid? BranchId { get; init; }
    public decimal? MaxPrice { get; init; }
    public bool PetSafeOnly { get; init; }
    public string? SkillLevel { get; init; }
    public RoomScanPipelineMode PipelineMode { get; init; }

    /// <summary>Optional chat notes passed to the ranker (budget, style, pet-safe emphasis, “different from before”).</summary>
    public string? RankRefinementNotes { get; init; }

    /// <summary>When set, prefer catalog items not in this set (e.g. previous scan suggestions).</summary>
    public IReadOnlyList<Guid>? ExcludeListingIds { get; init; }
}

public sealed class RoomScanCatalogRankingResult
{
    public List<RoomScanRecommendationDto> Recommendations { get; init; } = new();

    /// <summary>Set when no listings matched filters (caller may map to a user-facing disclaimer).</summary>
    public bool NoMatches { get; init; }
}
