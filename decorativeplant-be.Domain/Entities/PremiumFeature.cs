namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Defines available premium features and their availability per plan.
/// Maps to table: premium_feature
/// </summary>
public class PremiumFeature : BaseEntity
{
    public string FeatureKey { get; set; } = string.Empty; // Unique identifier, e.g., "ai_diagnosis"
    public string FeatureName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string AvailableInPlans { get; set; } = string.Empty; // JSON array: ["Free","Premium"]
    public bool IsActive { get; set; } = true;
}
