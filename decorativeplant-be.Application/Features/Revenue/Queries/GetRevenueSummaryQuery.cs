using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.DTOs.Revenue;

namespace decorativeplant_be.Application.Features.Revenue.Queries;

public record GetRevenueSummaryQuery(DateTime? From = null, DateTime? To = null, Guid? BranchId = null) : IRequest<RevenueSummaryDto>;

public class GetRevenueSummaryQueryHandler : IRequestHandler<GetRevenueSummaryQuery, RevenueSummaryDto>
{
    private readonly IApplicationDbContext _context;

    public GetRevenueSummaryQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<RevenueSummaryDto> Handle(GetRevenueSummaryQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.From ?? DateTime.UtcNow.AddYears(-1);
        var toDate = request.To ?? DateTime.UtcNow;

        decimal totalDiscount = 0;
        decimal totalOrderRevenue = 0; 
        int orderCount = 0;

        if (request.BranchId.HasValue)
        {
            // Filtered by Branch
            var branchId = request.BranchId.Value;
            
            // Get all paid/completed items for this branch
            var items = await _context.OrderItems
                .Include(oi => oi.Order)
                .Where(oi => oi.BranchId == branchId && 
                            (oi.Order!.Status == "Paid" || oi.Order!.Status == "Completed" || oi.Order!.Status == "paid" || oi.Order!.Status == "completed") &&
                            oi.Order.CreatedAt >= fromDate && oi.Order.CreatedAt <= toDate)
                .Select(oi => new { oi.OrderId, oi.Pricing, OrderFinancials = oi.Order!.Financials })
                .ToListAsync(cancellationToken);

            var ordersProcessed = new HashSet<Guid>();
            foreach (var item in items)
            {
                if (item.Pricing != null && item.Pricing.RootElement.TryGetProperty("subtotal", out var subProp) && 
                    decimal.TryParse(subProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sub))
                {
                    // Proportional discount calculation
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
                    totalDiscount += itemDiscount;
                    totalOrderRevenue += (sub - itemDiscount);
                    
                    if (item.OrderId.HasValue && !ordersProcessed.Contains(item.OrderId.Value))
                    {
                        ordersProcessed.Add(item.OrderId.Value);
                        orderCount++;
                    }
                }
            }
        }
        else
        {
            // Global Summary
            var orders = await _context.OrderHeaders
                .Where(o => (o.Status == "Paid" || o.Status == "Completed" || o.Status == "paid" || o.Status == "completed")
                         && o.CreatedAt >= fromDate && o.CreatedAt <= toDate)
                .Select(o => o.Financials)
                .ToListAsync(cancellationToken);

            orderCount = orders.Count;
            foreach (var financials in orders)
            {
                if (financials == null) continue;
                var root = financials.RootElement;
                
                if (root.TryGetProperty("discount", out var discProp) && decimal.TryParse(discProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var disc))
                    totalDiscount += disc;

                if (root.TryGetProperty("total", out var totalProp) && decimal.TryParse(totalProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var tot))
                    totalOrderRevenue += tot;
            }
        }

        // 2. Subscription Revenue (Always Total per user request)
        var subscriptionRevenueStr = await _context.UserSubscriptions
            .Where(s => (s.Status == "Active" || s.Status == "Expired") 
                     && s.CreatedAt >= fromDate && s.CreatedAt <= toDate)
            .Select(s => s.AmountPaid)
            .ToListAsync(cancellationToken);

        decimal totalSubscriptionRevenue = 0;
        foreach (var amtStr in subscriptionRevenueStr)
        {
            if (decimal.TryParse(amtStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amt))
                totalSubscriptionRevenue += amt;
        }

        var totalRevenue = totalOrderRevenue + totalSubscriptionRevenue;
        var avgOrderValue = orderCount > 0 ? totalOrderRevenue / orderCount : 0;

        return new RevenueSummaryDto
        {
            TotalRevenue = totalRevenue.ToString("0", CultureInfo.InvariantCulture),
            OrderRevenue = totalOrderRevenue.ToString("0", CultureInfo.InvariantCulture),
            SubscriptionRevenue = totalSubscriptionRevenue.ToString("0", CultureInfo.InvariantCulture),
            TotalDiscount = totalDiscount.ToString("0", CultureInfo.InvariantCulture),
            AvgOrderValue = avgOrderValue.ToString("0", CultureInfo.InvariantCulture)
        };
    }
}
