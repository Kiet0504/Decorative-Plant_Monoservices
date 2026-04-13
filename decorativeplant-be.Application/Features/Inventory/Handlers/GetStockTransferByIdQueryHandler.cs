using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class GetStockTransferByIdQueryHandler : IRequestHandler<GetStockTransferByIdQuery, StockTransferDto?>
{
    private readonly IApplicationDbContext _context;

    public GetStockTransferByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockTransferDto?> Handle(GetStockTransferByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.StockTransfers
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .Include(x => x.FromLocation)
            .Include(x => x.ToLocation)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (entity == null) return null;

        return InventoryMapper.ToStockTransferDto(entity);
    }
}
