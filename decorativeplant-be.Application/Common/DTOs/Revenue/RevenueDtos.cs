namespace decorativeplant_be.Application.Common.DTOs.Revenue;

public class RevenueSummaryDto
{
    public string TotalRevenue { get; set; } = "0";
    public string OrderRevenue { get; set; } = "0";
    public string SubscriptionRevenue { get; set; } = "0";
    public string TotalDiscount { get; set; } = "0";
    public string AvgOrderValue { get; set; } = "0";
}

public class MonthlyRevenueDto
{
    public string Month { get; set; } = "";
    public decimal Revenue { get; set; }
    public int OrderCount { get; set; }
}

public class BranchRevenueDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = "";
    public int OrderCount { get; set; }
    public string Address { get; set; } = "";
    public string OrderRevenue { get; set; } = "0";
    public string TotalRevenue { get; set; } = "0";
}

public class TopProductRevenueDto
{
    public string SpeciesName { get; set; } = "";
    public int UnitsSold { get; set; }
    public string UnitPrice { get; set; } = "0";
    public string TotalRevenue { get; set; } = "0";
    public string? ImageUrl { get; set; }
}
