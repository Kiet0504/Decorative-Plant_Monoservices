using MediatR;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.BranchManager.DTOs;
using decorativeplant_be.Application.Features.Revenue.Queries;
using decorativeplant_be.Application.Features.StoreStaff.DTOs;
using decorativeplant_be.Application.Features.StoreStaff.Queries;

namespace decorativeplant_be.Application.Features.BranchManager.Queries;

public record GetBranchManagerDashboardQuery(Guid BranchId) : IRequest<BranchManagerDashboardDto>;

public class GetBranchManagerDashboardQueryHandler : IRequestHandler<GetBranchManagerDashboardQuery, BranchManagerDashboardDto>
{
    private static readonly HashSet<string> PendingTransferStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "requested",
        "approved",
        "shipped",
    };

    private readonly IApplicationDbContext _context;
    private readonly IMediator _mediator;

    public GetBranchManagerDashboardQueryHandler(IApplicationDbContext context, IMediator mediator)
    {
        _context = context;
        _mediator = mediator;
    }

    public async Task<BranchManagerDashboardDto> Handle(GetBranchManagerDashboardQuery request, CancellationToken cancellationToken)
    {
        var branchId = request.BranchId;

        var baseDto = await _mediator.Send(new GetStoreStaffDashboardQuery(branchId), cancellationToken);

        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = DateTime.UtcNow;

        var revenueToday = await _mediator.Send(
            new GetRevenueSummaryQuery(todayStart, todayEnd, branchId),
            cancellationToken);

        var ordersCountToday = await _context.OrderHeaders.AsNoTracking()
            .Where(o => o.CreatedAt >= todayStart
                && o.CreatedAt <= todayEnd
                && o.OrderItems.Any(oi => oi.BranchId == branchId))
            .CountAsync(cancellationToken);

        var staffDistinct = await _context.StaffAssignments.AsNoTracking()
            .Where(s => s.BranchId == branchId)
            .Select(s => s.StaffId)
            .Distinct()
            .CountAsync(cancellationToken);

        var listingsCount = await _context.ProductListings.AsNoTracking()
            .CountAsync(p => p.BranchId == branchId, cancellationToken);

        var outgoing = await _context.StockTransfers.AsNoTracking()
            .CountAsync(t =>
                    t.FromBranchId == branchId
                    && t.Status != null
                    && PendingTransferStatuses.Contains(t.Status),
                cancellationToken);

        var incoming = await _context.StockTransfers.AsNoTracking()
            .CountAsync(t =>
                    t.ToBranchId == branchId
                    && t.Status != null
                    && PendingTransferStatuses.Contains(t.Status),
                cancellationToken);

        var recentTransferEntities = await _context.StockTransfers.AsNoTracking()
            .Include(t => t.FromBranch)
            .Include(t => t.ToBranch)
            .Where(t => t.FromBranchId == branchId || t.ToBranchId == branchId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(5)
            .ToListAsync(cancellationToken);

        var recentTransfers = recentTransferEntities.Select(t => new BranchManagerRecentTransferDto
        {
            Id = t.Id,
            TransferCode = t.TransferCode,
            Status = t.Status,
            FromBranchName = t.FromBranch?.Name,
            ToBranchName = t.ToBranch?.Name,
            Quantity = t.Quantity,
            CreatedAt = t.CreatedAt?.ToString("o"),
        }).ToList();

        var last30DaysSeries =
            await BranchDashboardChartSeriesBuilder.BuildLast30DaysSeries(_context, branchId, cancellationToken);
        var last14TransferSeries =
            await BranchDashboardChartSeriesBuilder.BuildLast14DaysTransferSeries(_context, branchId, cancellationToken);

        return Map(baseDto, new BranchManagerDashboardDto
        {
            OrderRevenueToday = revenueToday.OrderRevenue,
            OrdersCountToday = ordersCountToday,
            StaffAssignedCount = staffDistinct,
            ProductListingsCount = listingsCount,
            PendingTransfersOutgoingCount = outgoing,
            PendingTransfersIncomingCount = incoming,
            RecentTransfers = recentTransfers,
            Last30DaysSeries = last30DaysSeries,
            Last14DaysTransferSeries = last14TransferSeries,
        });
    }

    private static BranchManagerDashboardDto Map(StoreStaffDashboardDto src, BranchManagerDashboardDto dest)
    {
        dest.BranchId = src.BranchId;
        dest.BranchName = src.BranchName;
        dest.PeriodDays = src.PeriodDays;
        dest.OrderRevenue30d = src.OrderRevenue30d;
        dest.OrdersCount30d = src.OrdersCount30d;
        dest.ActiveOrdersCount30d = src.ActiveOrdersCount30d;
        dest.LowStockCount = src.LowStockCount;
        dest.LowStockPreview = src.LowStockPreview;
        dest.PendingStockTransfersCount = src.PendingStockTransfersCount;
        dest.IotDeviceCount = src.IotDeviceCount;
        dest.RecentOrders = src.RecentOrders;
        return dest;
    }
}
