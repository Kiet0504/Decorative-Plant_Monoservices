using System.Globalization;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.BranchManager.DTOs;

namespace decorativeplant_be.Application.Features.BranchManager.Queries;

internal static class BranchDashboardChartSeriesBuilder
{
    /// <summary>Build last 30 UTC days (inclusive of today) with order volume + branch-attributed revenue (paid/completed lines).</summary>
    public static async Task<List<BranchDailyChartPointDto>> BuildLast30DaysSeries(
        IApplicationDbContext context,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-29);
        var toExclusive = today.AddDays(1);

        var orderRows = await context.OrderHeaders.AsNoTracking()
            .Where(o => o.CreatedAt != null
                        && o.CreatedAt >= from
                        && o.CreatedAt < toExclusive
                        && o.OrderItems.Any(oi => oi.BranchId == branchId))
            .Select(o => new { o.Id, CreatedAt = o.CreatedAt!.Value })
            .ToListAsync(cancellationToken);

        var ordersByDay = orderRows
            .GroupBy(x => x.CreatedAt.Date)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Id).Distinct().Count());

        var revenueItems = await context.OrderItems.AsNoTracking()
            .Include(oi => oi.Order)
            .Where(oi => oi.BranchId == branchId
                         && oi.Order != null
                         && oi.Order.CreatedAt >= from
                         && oi.Order.CreatedAt < toExclusive
                         && (oi.Order.Status == "Paid" || oi.Order.Status == "Completed"
                             || oi.Order.Status == "paid" || oi.Order.Status == "completed"))
            .Select(oi => new
            {
                oi.OrderId,
                CreatedAt = oi.Order!.CreatedAt,
                oi.Pricing,
                OrderFinancials = oi.Order.Financials,
            })
            .ToListAsync(cancellationToken);

        var revenueByDay = new Dictionary<DateTime, decimal>();
        foreach (var item in revenueItems)
        {
            if (item.CreatedAt == null) continue;
            var day = item.CreatedAt.Value.Date;
            var net = NetRevenueFromOrderItem(item.Pricing, item.OrderFinancials);
            if (!revenueByDay.ContainsKey(day)) revenueByDay[day] = 0;
            revenueByDay[day] += net;
        }

        var list = new List<BranchDailyChartPointDto>();
        for (var d = from; d < toExclusive; d = d.AddDays(1))
        {
            ordersByDay.TryGetValue(d, out var oc);
            revenueByDay.TryGetValue(d, out var rev);
            list.Add(new BranchDailyChartPointDto
            {
                Date = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                OrdersCount = oc,
                Revenue = rev.ToString("0", CultureInfo.InvariantCulture),
            });
        }

        return list;
    }

    public static async Task<List<BranchTransferChartPointDto>> BuildLast14DaysTransferSeries(
        IApplicationDbContext context,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var from = today.AddDays(-13);
        var toExclusive = today.AddDays(1);

        var days = await context.StockTransfers.AsNoTracking()
            .Where(t => t.CreatedAt != null
                        && t.CreatedAt >= from
                        && t.CreatedAt < toExclusive
                        && (t.FromBranchId == branchId || t.ToBranchId == branchId))
            .Select(t => t.CreatedAt!.Value.Date)
            .ToListAsync(cancellationToken);

        var counts = days.GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());

        var list = new List<BranchTransferChartPointDto>();
        for (var d = from; d < toExclusive; d = d.AddDays(1))
        {
            counts.TryGetValue(d, out var c);
            list.Add(new BranchTransferChartPointDto
            {
                Date = d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                TransferCount = c,
            });
        }

        return list;
    }

    private static decimal NetRevenueFromOrderItem(
        System.Text.Json.JsonDocument? pricing,
        System.Text.Json.JsonDocument? orderFinancials)
    {
        if (pricing?.RootElement.TryGetProperty("subtotal", out var subProp) != true
            || !decimal.TryParse(subProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sub))
            return 0;

        decimal orderSubtotal = 0;
        decimal orderDiscount = 0;
        if (orderFinancials != null)
        {
            var root = orderFinancials.RootElement;
            if (root.TryGetProperty("subtotal", out var osProp)
                && decimal.TryParse(osProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var os))
                orderSubtotal = os;
            if (root.TryGetProperty("discount", out var odProp)
                && decimal.TryParse(odProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var od))
                orderDiscount = od;
        }

        var itemDiscount = orderSubtotal > 0 ? (sub / orderSubtotal) * orderDiscount : 0;
        return sub - itemDiscount;
    }
}
