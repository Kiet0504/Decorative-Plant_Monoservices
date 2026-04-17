using decorativeplant_be.Application.Common.DTOs.RoomScan;

namespace decorativeplant_be.Application.Common.Interfaces;

public interface IRoomScanGeminiClient
{
    /// <summary>Multimodal: infer room constraints from image.</summary>
    Task<RoomProfileDto?> AnalyzeRoomFromImageAsync(
        string imageBase64,
        string mimeType,
        RoomScanPipelineMode pipelineMode,
        CancellationToken cancellationToken = default);

    /// <summary>Text-only: rank catalog listing ids (must exist in snippets).</summary>
    /// <param name="rankRefinementNotes">Optional user chat constraints (e.g. cheaper, pet-safe, different picks).</param>
    Task<IReadOnlyList<RoomScanGeminiRankItem>?> RankListingsAsync(
        RoomProfileDto roomProfile,
        IReadOnlyList<RoomScanCatalogSnippet> snippets,
        bool petSafeOnly,
        string? skillLevel,
        int maxRecommendations,
        RoomScanPipelineMode pipelineMode,
        string? rankRefinementNotes = null,
        CancellationToken cancellationToken = default);
}

public sealed class RoomScanCatalogSnippet
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "";
    public string CareSummary { get; init; } = "";
    public List<string> Tags { get; init; } = new();
}

public sealed class RoomScanGeminiRankItem
{
    public Guid ListingId { get; init; }
    public int Rank { get; init; }
    public string Reason { get; init; } = "";
}
