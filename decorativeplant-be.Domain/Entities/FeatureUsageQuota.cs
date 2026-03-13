namespace decorativeplant_be.Domain.Entities;

/// <summary>
/// Tracks feature usage quotas per user (e.g., AI diagnosis limit, plant count limit).
/// Maps to table: feature_usage_quota
/// </summary>
public class FeatureUsageQuota : BaseEntity
{
    public Guid UserId { get; set; }
    public string FeatureKey { get; set; } = string.Empty; // e.g., "ai_diagnosis", "plant_count", "care_guide"
    public int QuotaLimit { get; set; }
    public int QuotaUsed { get; set; } = 0;
    public string QuotaPeriod { get; set; } = string.Empty; // "Monthly" | "Yearly" | "Unlimited"
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }

    // Navigation
    public UserAccount User { get; set; } = null!;
}
