using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Linq.Expressions;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ListStockTransfersQueryHandler : IRequestHandler<ListStockTransfersQuery, PagedResultDto<StockTransferDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public ListStockTransfersQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<PagedResultDto<StockTransferDto>> Handle(ListStockTransfersQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<StockTransfer>();

        Expression<Func<StockTransfer, bool>> filter = x =>
            (string.IsNullOrEmpty(request.Status) || x.Status == request.Status) &&
            (!request.BatchId.HasValue || x.BatchId == request.BatchId) &&
            (!request.FromBranchId.HasValue || x.FromBranchId == request.FromBranchId) &&
            (!request.ToBranchId.HasValue || x.ToBranchId == request.ToBranchId);

        var totalCount = await repo.CountAsync(filter, cancellationToken);
        var allItems = await repo.FindAsync(filter, cancellationToken);
        
        // Manual paging as before
        var pagedItems = allItems.OrderByDescending(x => x.CreatedAt)
                                 .Skip((request.Page - 1) * request.PageSize)
                                 .Take(request.PageSize)
                                 .ToList();

        var dtos = pagedItems.Select(InventoryMapper.ToStockTransferDto).ToList();

        return new PagedResultDto<StockTransferDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            Page = request.Page,
            PageSize = request.PageSize
        };
    }
}
