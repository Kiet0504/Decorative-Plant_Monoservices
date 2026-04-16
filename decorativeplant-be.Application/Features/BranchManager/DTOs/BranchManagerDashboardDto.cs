using decorativeplant_be.Application.Features.StoreStaff.DTOs;

namespace decorativeplant_be.Application.Features.BranchManager.DTOs;

/// <summary>
/// Branch manager dashboard: everything in <see cref="StoreStaffDashboardDto"/> plus oversight metrics.
/// </summary>
public class BranchManagerDashboardDto : StoreStaffDashboardDto
{
    /// <summary>Distinct staff users assigned to this branch.</summary>
    public int StaffAssignedCount { get; set; }

    /// <summary>Product listing rows for this branch.</summary>
    public int ProductListingsCount { get; set; }

    /// <summary>Pending transfers where this branch is the sender.</summary>
    public int PendingTransfersOutgoingCount { get; set; }

    /// <summary>Pending transfers where this branch is the receiver.</summary>
    public int PendingTransfersIncomingCount { get; set; }

    /// <summary>Distinct orders with a line for this branch, created today (UTC).</summary>
    public int OrdersCountToday { get; set; }

    /// <summary>Branch revenue for today (UTC), same rules as <see cref="StoreStaffDashboardDto.OrderRevenue30d"/> but 1-day window.</summary>
    public string OrderRevenueToday { get; set; } = "0";

    public List<BranchManagerRecentTransferDto> RecentTransfers { get; set; } = new();

    /// <summary>UTC calendar days for charting: last 30 days including today; gaps use zeros.</summary>
    public List<BranchDailyChartPointDto> Last30DaysSeries { get; set; } = new();

    /// <summary>Stock transfers involving this branch per UTC day (last 14 days).</summary>
    public List<BranchTransferChartPointDto> Last14DaysTransferSeries { get; set; } = new();
}

/// <summary>Daily branch metrics for charts (orders = any status with branch line; revenue = paid/completed branch lines).</summary>
public class BranchDailyChartPointDto
{
    public string Date { get; set; } = "";
    public int OrdersCount { get; set; }
    public string Revenue { get; set; } = "0";
}

public class BranchTransferChartPointDto
{
    public string Date { get; set; } = "";
    public int TransferCount { get; set; }
}

public class BranchManagerRecentTransferDto
{
    public Guid Id { get; set; }
    public string? TransferCode { get; set; }
    public string? Status { get; set; }
    public string? FromBranchName { get; set; }
    public string? ToBranchName { get; set; }
    public int Quantity { get; set; }
    public string? CreatedAt { get; set; }
}
