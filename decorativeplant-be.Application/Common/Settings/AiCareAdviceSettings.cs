namespace decorativeplant_be.Application.Common.Settings;

public sealed class AiCareAdviceSettings
{
    public const string SectionName = "AiCare";

    /// <summary>
    /// Cache TTL in hours. If <= 0, caching is disabled.
    /// </summary>
    public int CacheTtlHours { get; set; } = 24;
}

