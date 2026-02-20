using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Queries;

public class GetLowStockQuery : IRequest<List<LowStockItemDto>>
{
    public Guid? BranchId { get; set; }
    public int Threshold { get; set; } = 10;
}
