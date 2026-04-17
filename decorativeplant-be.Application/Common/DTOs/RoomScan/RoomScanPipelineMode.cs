namespace decorativeplant_be.Application.Common.DTOs.RoomScan;

/// <summary>How room scan uses cloud vs local AI for this request.</summary>
public enum RoomScanPipelineMode
{
    /// <summary>Gemini for room photo first, Ollama vision as fallback; catalog rank uses server <c>RankProvider</c>.</summary>
    Hybrid = 0,

    /// <summary>No Gemini: Ollama vision for photo + Ollama for ranking on this request.</summary>
    LocalOnly = 1
}

public static class RoomScanPipelineModeParser
{
    /// <summary>Parses config or env <c>RoomScan:PipelineMode</c> (e.g. <c>hybrid</c>, <c>localOnly</c>).</summary>
    public static RoomScanPipelineMode FromApiValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return RoomScanPipelineMode.Hybrid;
        }

        var t = value.Trim();
        if (string.Equals(t, "localOnly", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(t, "local", StringComparison.OrdinalIgnoreCase))
        {
            return RoomScanPipelineMode.LocalOnly;
        }

        return RoomScanPipelineMode.Hybrid;
    }
}
