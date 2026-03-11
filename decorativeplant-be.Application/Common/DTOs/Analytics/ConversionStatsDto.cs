namespace decorativeplant_be.Application.Common.DTOs.Analytics;

/// <summary>
/// Statistics about subscription conversions from Free to Premium.
/// </summary>
public class ConversionStatsDto
{
    /// <summary>Total number of distinct users with subscriptions</summary>
    public int TotalUsers { get; set; }

    /// <summary>Number of users with active Free subscriptions</summary>
    public int FreeUsers { get; set; }

    /// <summary>Number of users with active Premium subscriptions</summary>
    public int PremiumUsers { get; set; }

    /// <summary>Percentage of users who upgraded to Premium (PremiumUsers / TotalUsers * 100)</summary>
    public decimal ConversionRate { get; set; }

    /// <summary>Monthly breakdown of premium upgrades</summary>
    public List<MonthlyUpgradeDto> UpgradesPerMonth { get; set; } = new();
}

/// <summary>
/// Monthly upgrade statistics.
/// </summary>
public class MonthlyUpgradeDto
{
    /// <summary>Month (1-12)</summary>
    public int Month { get; set; }

    /// <summary>Year (e.g., 2026)</summary>
    public int Year { get; set; }

    /// <summary>Number of upgrades in this month</summary>
    public int Count { get; set; }
}
