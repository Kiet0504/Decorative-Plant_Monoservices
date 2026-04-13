using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ListStockTransfersQueryHandler : IRequestHandler<ListStockTransfersQuery, PagedResultDto<StockTransferDto>>
{
    private readonly IApplicationDbContext _context;

    public ListStockTransfersQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResultDto<StockTransferDto>> Handle(ListStockTransfersQuery request, CancellationToken cancellationToken)
    {
        var query = _context.StockTransfers
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .Where(x =>
                (string.IsNullOrEmpty(request.Status) || x.Status == request.Status) &&
                (!request.BatchId.HasValue || x.BatchId == request.BatchId) &&
                (!request.FromBranchId.HasValue || x.FromBranchId == request.FromBranchId) &&
                (!request.ToBranchId.HasValue || x.ToBranchId == request.ToBranchId));

        var totalCount = await query.CountAsync(cancellationToken);
        
        var items = await query.OrderByDescending(x => x.CreatedAt)
                               .Skip((request.Page - 1) * request.PageSize)
                               .Take(request.PageSize)
                               .ToListAsync(cancellationToken);

        var dtos = items.Select(InventoryMapper.ToStockTransferDto).ToList();

        return new PagedResultDto<StockTransferDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
