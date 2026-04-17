namespace decorativeplant_be.Application.Common.Settings;

/// <summary>Restricts /ai/chat to plant-care, shop, garden, and diagnosis topics.</summary>
public sealed class PlantAssistantScopeSettings
{
    public const string SectionName = "PlantAssistantScope";

    /// <summary>When false, domain gate is skipped (not recommended for production).</summary>
    public bool Enabled { get; set; } = true;
}
