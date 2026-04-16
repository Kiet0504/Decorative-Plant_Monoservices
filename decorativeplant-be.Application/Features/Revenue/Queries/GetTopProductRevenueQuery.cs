using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.DTOs.Revenue;

namespace decorativeplant_be.Application.Features.Revenue.Queries;

public record GetTopProductRevenueQuery(int TopCount = 10, DateTime? From = null, DateTime? To = null, Guid? BranchId = null) : IRequest<List<TopProductRevenueDto>>;

public class GetTopProductRevenueQueryHandler : IRequestHandler<GetTopProductRevenueQuery, List<TopProductRevenueDto>>
{
    private readonly IApplicationDbContext _context;

    public GetTopProductRevenueQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<TopProductRevenueDto>> Handle(GetTopProductRevenueQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.From ?? DateTime.UtcNow.AddYears(-1);
        var toDate = request.To ?? DateTime.UtcNow;

        var query = _context.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => (oi.Order!.Status == "Paid" || oi.Order!.Status == "Completed" || oi.Order!.Status == "paid" || oi.Order!.Status == "completed")
                     && oi.Order!.CreatedAt >= fromDate && oi.Order!.CreatedAt <= toDate);

        if (request.BranchId.HasValue)
        {
            query = query.Where(oi => oi.BranchId == request.BranchId.Value);
        }

        var orderItems = await query.ToListAsync(cancellationToken);

        var topProducts = orderItems
            .GroupBy(oi => oi.ListingId)
            .Select(g => {
                var firstItem = g.First();
                decimal productRevenue = 0;
                int unitsSold = 0;
                string speciesName = "Unknown";
                string unitPrice = "0";

                if (firstItem.Snapshots != null && firstItem.Snapshots.RootElement.TryGetProperty("title_snapshot", out var titleProp))
                    speciesName = titleProp.GetString() ?? "Unknown";

                if (firstItem.Pricing != null && firstItem.Pricing.RootElement.TryGetProperty("unit_price", out var upProp))
                    unitPrice = upProp.GetString() ?? "0";

                foreach (var item in g)
                {
                    unitsSold += item.Quantity;
                    if (item.Pricing != null && 
                        item.Pricing.RootElement.TryGetProperty("subtotal", out var subProp) && 
                        decimal.TryParse(subProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sub))
                    {
                        productRevenue += sub;
                    }
                }

                return new TopProductRevenueDto
                {
                    SpeciesName = speciesName,
                    UnitsSold = unitsSold,
                    UnitPrice = unitPrice,
                    TotalRevenue = productRevenue.ToString("0", CultureInfo.InvariantCulture)
                };
            })
            .OrderByDescending(p => decimal.Parse(p.TotalRevenue))
            .Take(request.TopCount)
            .ToList();

        return topProducts;
    }
}
