using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Commerce.Orders;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Application.Features.Revenue.Queries;
using decorativeplant_be.Application.Features.StoreStaff.DTOs;

namespace decorativeplant_be.Application.Features.StoreStaff.Queries;

public record GetStoreStaffDashboardQuery(Guid BranchId) : IRequest<StoreStaffDashboardDto>;

public class GetStoreStaffDashboardQueryHandler : IRequestHandler<GetStoreStaffDashboardQuery, StoreStaffDashboardDto>
{
    private const int PeriodDays = 30;
    private const int LowStockThreshold = 10;

    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public GetStoreStaffDashboardQueryHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<StoreStaffDashboardDto> Handle(GetStoreStaffDashboardQuery request, CancellationToken cancellationToken)
    {
        var branchId = request.BranchId;
        var branch = await _context.Branches.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == branchId, cancellationToken)
            ?? throw new KeyNotFoundException($"Branch {branchId} not found.");

        var since = DateTime.UtcNow.AddDays(-PeriodDays);

        var revenue = await _mediator.Send(
            new GetRevenueSummaryQuery(since, DateTime.UtcNow, branchId),
            cancellationToken);

        var statusesInPeriod = await _context.OrderHeaders.AsNoTracking()
            .Where(o => o.CreatedAt >= since
                && o.OrderItems.Any(oi => oi.BranchId == branchId))
            .Select(o => o.Status)
            .ToListAsync(cancellationToken);

        var ordersCount = statusesInPeriod.Count;
        var activeOrders = statusesInPeriod.Count(s => !OrderStatusMachine.IsTerminal(s));

        var recentEntities = await _context.OrderHeaders.AsNoTracking()
            .Where(o => o.OrderItems.Any(oi => oi.BranchId == branchId))
            .OrderByDescending(o => o.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentOrders = recentEntities
            .Select(o => new StoreStaffRecentOrderDto
            {
                Id = o.Id,
                OrderCode = o.OrderCode,
                Status = o.Status ?? "",
                CreatedAt = o.CreatedAt?.ToString("o"),
                Total = o.Financials?.RootElement.TryGetProperty("total", out var t) == true
                    ? t.GetString()
                    : null,
            })
            .ToList();

        var lowStockList = await _mediator.Send(
            new GetLowStockQuery { BranchId = branchId, Threshold = LowStockThreshold },
            cancellationToken);

        var lowRisk = lowStockList
            .Where(x => x.CurrentStock <= LowStockThreshold)
            .ToList();

        var lowPreview = lowRisk
            .Take(5)
            .Select(x => new StoreStaffLowStockPreviewDto
            {
                ProductId = x.ProductId,
                ProductName = x.ProductName,
                Category = x.Category,
                CurrentStock = x.CurrentStock,
                Threshold = x.Threshold,
            })
            .ToList();

        var pendingTransfers = await _context.StockTransfers.AsNoTracking()
            .CountAsync(t =>
                (t.FromBranchId == branchId || t.ToBranchId == branchId)
                && t.Status != null
                && PendingTransferStatuses.Contains(t.Status),
                cancellationToken);

        var iotCount = await _context.IotDevices.AsNoTracking()
            .CountAsync(d => d.BranchId == branchId, cancellationToken);

        return new StoreStaffDashboardDto
        {
            BranchId = branchId,
            BranchName = branch.Name,
            PeriodDays = PeriodDays,
            OrderRevenue30d = revenue.OrderRevenue,
            OrdersCount30d = ordersCount,
            ActiveOrdersCount30d = activeOrders,
            LowStockCount = lowRisk.Count,
            LowStockPreview = lowPreview,
            PendingStockTransfersCount = pendingTransfers,
            IotDeviceCount = iotCount,
            RecentOrders = recentOrders,
        };
    }

    /// <summary>Transfer rows still in motion for this branch (case-insensitive status).</summary>
    private static readonly HashSet<string> PendingTransferStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "requested",
        "approved",
        "shipped",
    };
}
