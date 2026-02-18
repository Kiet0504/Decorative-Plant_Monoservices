using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Queries;

public class ListStockTransfersQuery : IRequest<PagedResultDto<StockTransferDto>>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? Status { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? FromBranchId { get; set; }
    public Guid? ToBranchId { get; set; }
}
