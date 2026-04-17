namespace decorativeplant_be.Application.Common.Settings;

/// <summary>Bound from RoomScan configuration (shared section name with Infrastructure Gemini overrides).</summary>
public sealed class RoomScanHandlerOptions
{
    public const string SectionName = "RoomScan";

    public int MaxCatalogSnippets { get; set; } = 48;
    public int MaxRecommendations { get; set; } = 5;

    /// <summary>
    /// <c>hybrid</c> (default): Gemini for room photo when configured, then Ollama vision if needed; ranking uses <c>RankProvider</c>.
    /// <c>localOnly</c>: no Gemini — Ollama vision + Ollama ranking (server-wide).
    /// </summary>
    public string PipelineMode { get; set; } = "hybrid";
}
