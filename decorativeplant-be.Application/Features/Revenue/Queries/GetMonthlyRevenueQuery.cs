using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.DTOs.Revenue;

namespace decorativeplant_be.Application.Features.Revenue.Queries;

public record GetMonthlyRevenueQuery(DateTime? From = null, DateTime? To = null, Guid? BranchId = null) : IRequest<List<MonthlyRevenueDto>>;

public class GetMonthlyRevenueQueryHandler : IRequestHandler<GetMonthlyRevenueQuery, List<MonthlyRevenueDto>>
{
    private readonly IApplicationDbContext _context;

    public GetMonthlyRevenueQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<MonthlyRevenueDto>> Handle(GetMonthlyRevenueQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.From ?? DateTime.UtcNow.AddMonths(-6);
        var toDate = request.To ?? DateTime.UtcNow;

        List<MonthlyDataPoint> points;

        if (request.BranchId.HasValue)
        {
            var branchId = request.BranchId.Value;
            var items = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.BranchId == branchId && 
                            (oi.Order!.Status == "Paid" || oi.Order!.Status == "Completed" || oi.Order!.Status == "paid" || oi.Order!.Status == "completed") &&
                            oi.Order.CreatedAt >= fromDate && oi.Order.CreatedAt <= toDate)
                .Select(oi => new { oi.OrderId, oi.Order!.CreatedAt, oi.Pricing, OrderFinancials = oi.Order.Financials, OrderNotes = oi.Order.Notes })
                .ToListAsync(cancellationToken);

            points = items.Select(item => {
                decimal netRevenue = 0;
                if (item.Pricing != null && item.Pricing.RootElement.TryGetProperty("subtotal", out var subProp) &&
                    decimal.TryParse(subProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sub))
                {
                    decimal orderSubtotal = 0;
                    decimal orderDiscount = 0;
                    if (item.OrderFinancials != null)
                    {
                        var root = item.OrderFinancials.RootElement;
                        if (root.TryGetProperty("subtotal", out var osProp) && decimal.TryParse(osProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var os))
                            orderSubtotal = os;
                        if (root.TryGetProperty("discount", out var odProp) && decimal.TryParse(odProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var od))
                            orderDiscount = od;
                    }
                    decimal itemDiscount = orderSubtotal > 0 ? (sub / orderSubtotal) * orderDiscount : 0;
                    var scale = RevenueAdjustments.CodScaleFactor(item.OrderNotes, item.OrderFinancials);
                    netRevenue = (sub - itemDiscount) * scale;
                }
                return new MonthlyDataPoint { CreatedAt = item.CreatedAt, Revenue = netRevenue, OrderId = item.OrderId ?? Guid.Empty };
            }).ToList();
        }
        else
        {
            var orders = await _context.OrderHeaders
                .Where(o => (o.Status == "Paid" || o.Status == "Completed" || o.Status == "paid" || o.Status == "completed")
                         && o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                .Select(o => new { o.Id, o.CreatedAt, o.Financials, o.Notes })
                .ToListAsync(cancellationToken);

            points = orders.Select(o => {
                decimal rev = 0;
                var ov = RevenueAdjustments.TryReadCodOverride(o.Notes);
                if (ov.HasValue) rev = ov.Value;
                else if (o.Financials != null && o.Financials.RootElement.TryGetProperty("total", out var totProp) &&
                    decimal.TryParse(totProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var tot))
                    rev = tot;
                return new MonthlyDataPoint { CreatedAt = o.CreatedAt, Revenue = rev, OrderId = o.Id };
            }).ToList();
        }

        // Add Import Costs from Plant Batches
        var batchPoints = await _context.PlantBatches
            .Where(b => (request.BranchId == null || b.BranchId == request.BranchId) &&
                        b.CreatedAt >= fromDate && b.CreatedAt <= toDate)
            .Select(b => new { b.CreatedAt, b.SourceInfo })
            .ToListAsync(cancellationToken);

        foreach (var bp in batchPoints)
        {
            decimal cost = 0;
            if (bp.SourceInfo != null && bp.SourceInfo.RootElement.TryGetProperty("purchase_cost", out var costProp))
            {
                if (costProp.ValueKind == JsonValueKind.Number && costProp.TryGetDecimal(out var dVal))
                    cost = dVal;
                else if (costProp.ValueKind == JsonValueKind.String && decimal.TryParse(costProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sVal))
                    cost = sVal;
            }
            points.Add(new MonthlyDataPoint { CreatedAt = bp.CreatedAt, Revenue = 0, ImportCost = cost, OrderId = Guid.Empty });
        }

        var monthlyData = points
            .GroupBy(p => new { p.CreatedAt!.Value.Year, p.CreatedAt.Value.Month })
            .Select(g => {
                var rev = g.Sum(x => x.Revenue);
                var cost = g.Sum(x => x.ImportCost);
                return new MonthlyRevenueDto
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM"),
                    Revenue = rev,
                    ImportCost = cost,
                    NetProfit = rev - cost,
                    OrderCount = g.Where(x => x.OrderId != Guid.Empty).Select(x => x.OrderId).Distinct().Count()
                };
            })
            .ToList();

        return monthlyData
            .OrderBy(m => DateTime.ParseExact(m.Month, "MMM", CultureInfo.InvariantCulture).Month)
            .ToList();
    }

    private class MonthlyDataPoint
    {
        public DateTime? CreatedAt { get; set; }
        public decimal Revenue { get; set; }
        public decimal ImportCost { get; set; }
        public Guid OrderId { get; set; }
    }
}
