namespace decorativeplant_be.Application.Common.Settings;

/// <summary>User-originated text checks before sending content to local or cloud models.</summary>
public sealed class ContentSafetySettings
{
    public const string SectionName = "ContentSafety";

    /// <summary>When false, all checks are skipped (e.g. local debugging).</summary>
    public bool Enabled { get; set; } = true;
}
