using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class RequestStockTransferCommand : IRequest<StockTransferDto>
{
    public Guid BatchId { get; set; }
    public Guid FromBranchId { get; set; }
    public Guid ToBranchId { get; set; }
    public Guid FromLocationId { get; set; }
    public Guid ToLocationId { get; set; }
    public int Quantity { get; set; }
    public string? Notes { get; set; }
    public Guid? RequestedBy { get; set; }
}
