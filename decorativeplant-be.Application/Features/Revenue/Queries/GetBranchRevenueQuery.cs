using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Globalization;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.DTOs.Revenue;

namespace decorativeplant_be.Application.Features.Revenue.Queries;

public record GetBranchRevenueQuery(DateTime? From = null, DateTime? To = null, Guid? BranchId = null) : IRequest<List<BranchRevenueDto>>;

public class GetBranchRevenueQueryHandler : IRequestHandler<GetBranchRevenueQuery, List<BranchRevenueDto>>
{
    private readonly IApplicationDbContext _context;

    public GetBranchRevenueQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<BranchRevenueDto>> Handle(GetBranchRevenueQuery request, CancellationToken cancellationToken)
    {
        var fromDate = request.From ?? DateTime.UtcNow.AddYears(-1);
        var toDate = request.To ?? DateTime.UtcNow;

        var query = _context.OrderItems
            .Include(oi => oi.Branch)
            .Include(oi => oi.Order)
            .Where(oi => (oi.Order!.Status == "Paid" || oi.Order!.Status == "Completed" || oi.Order!.Status == "paid" || oi.Order!.Status == "completed")
                     && oi.Order!.CreatedAt >= fromDate && oi.Order!.CreatedAt <= toDate);

        if (request.BranchId.HasValue)
        {
            query = query.Where(oi => oi.BranchId == request.BranchId.Value);
        }

        var orderItems = await query.ToListAsync(cancellationToken);

        var branchData = orderItems
            .Where(oi => oi.BranchId.HasValue)
            .GroupBy(oi => oi.BranchId)
            .Select(g => {
                var branch = g.First().Branch;
                decimal grossRevenue = 0;
                foreach (var item in g)
                {
                    if (item.Pricing != null && 
                        item.Pricing.RootElement.TryGetProperty("subtotal", out var subProp) && 
                        decimal.TryParse(subProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sub))
                    {
                        grossRevenue += sub;
                    }
                }

                string address = "Unknown Location";
                if (branch?.ContactInfo != null && branch.ContactInfo.RootElement.TryGetProperty("city", out var cityProp))
                {
                    address = cityProp.GetString() ?? "Unknown Location";
                }

                return new BranchRevenueDto
                {
                    BranchId = g.Key!.Value,
                    BranchName = branch?.Name ?? "Unknown Branch",
                    OrderCount = g.Select(oi => oi.OrderId).Distinct().Count(),
                    Address = address,
                    OrderRevenue = grossRevenue.ToString("0", CultureInfo.InvariantCulture),
                    TotalRevenue = grossRevenue.ToString("0", CultureInfo.InvariantCulture)
                };
            })
            .OrderByDescending(b => decimal.Parse(b.TotalRevenue))
            .ToList();

        return branchData;
    }
}
