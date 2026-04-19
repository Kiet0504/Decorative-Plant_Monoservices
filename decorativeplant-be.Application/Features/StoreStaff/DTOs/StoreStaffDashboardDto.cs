namespace decorativeplant_be.Application.Features.StoreStaff.DTOs;

public class StoreStaffDashboardDto
{
    public Guid BranchId { get; set; }
    public string BranchName { get; set; } = "";

    /// <summary>Rolling window in days used for order counts and revenue (UTC).</summary>
    public int PeriodDays { get; set; } = 30;

    /// <summary>Branch-attributed order revenue for the period (from paid/completed logic in revenue summary).</summary>
    public string OrderRevenue30d { get; set; } = "0";

    /// <summary>Distinct orders (any line item for this branch) in the period.</summary>
    public int OrdersCount30d { get; set; }

    /// <summary>Subset of <see cref="OrdersCount30d"/> that are not in a terminal state (needs staff/customer follow-up).</summary>
    public int ActiveOrdersCount30d { get; set; }

    public int LowStockCount { get; set; }
    public List<StoreStaffLowStockPreviewDto> LowStockPreview { get; set; } = new();

    public int PendingStockTransfersCount { get; set; }
    public int IotDeviceCount { get; set; }

    public List<StoreStaffRecentOrderDto> RecentOrders { get; set; } = new();
}

public class StoreStaffLowStockPreviewDto
{
    public string ProductId { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Category { get; set; } = "";
    public int CurrentStock { get; set; }
    public int Threshold { get; set; }
}

public class StoreStaffRecentOrderDto
{
    public Guid Id { get; set; }
    public string? OrderCode { get; set; }
    public string Status { get; set; } = "";
    public string? CreatedAt { get; set; }
    public string? Total { get; set; }
    public Guid? AssignedStaffId { get; set; }
    public string? AssignedStaffName { get; set; }
}
