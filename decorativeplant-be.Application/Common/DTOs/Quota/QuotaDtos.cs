namespace decorativeplant_be.Application.Common.DTOs.Quota;

// ── Request DTOs ──
public class ConsumeQuotaRequest
{
    public string FeatureKey { get; set; } = string.Empty;
}

public class CheckQuotaRequest
{
    public string FeatureKey { get; set; } = string.Empty;
}

// ── Response DTOs ──
public class QuotaStatusDto
{
    public string FeatureKey { get; set; } = string.Empty;
    public int QuotaLimit { get; set; }
    public int QuotaUsed { get; set; }
    public int RemainingCount { get; set; }
    public string QuotaPeriod { get; set; } = string.Empty;
    public DateTime PeriodEndDate { get; set; }
    public bool IsNearLimit { get; set; } // >= 80%
}

public class ConsumeQuotaResponse
{
    public string FeatureKey { get; set; } = string.Empty;
    public int QuotaUsed { get; set; }
    public int QuotaLimit { get; set; }
    public int RemainingCount { get; set; }
    public DateTime PeriodEndDate { get; set; }
    public string? Message { get; set; }
}

public class CheckQuotaResponse
{
    public bool CanUse { get; set; }
    public int Remaining { get; set; }
    public bool LimitReached { get; set; }
}

public class QuotaSummaryDto
{
    public string FeatureKey { get; set; } = string.Empty;
    public int AffectedUserCount { get; set; }
    public double AverageUsagePercent { get; set; }
}
