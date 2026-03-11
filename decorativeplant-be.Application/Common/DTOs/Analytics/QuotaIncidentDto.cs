namespace decorativeplant_be.Application.Common.DTOs.Analytics;

/// <summary>
/// Quota usage incident data for features with high usage (>= 80%).
/// </summary>
public class QuotaIncidentDto
{
    /// <summary>Feature key identifier</summary>
    public string FeatureKey { get; set; } = string.Empty;

    /// <summary>Total number of users affected by high quota usage</summary>
    public int TotalAffectedUsers { get; set; }

    /// <summary>Average usage percentage across affected users</summary>
    public decimal AvgUsagePercent { get; set; }

    /// <summary>Maximum usage percentage among affected users</summary>
    public decimal MaxUsagePercent { get; set; }
}
