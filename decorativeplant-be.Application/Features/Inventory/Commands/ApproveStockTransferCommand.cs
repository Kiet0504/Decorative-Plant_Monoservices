using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class ApproveStockTransferCommand : IRequest<StockTransferDto>
{
    public Guid TransferId { get; set; }
    public bool Approved { get; set; }
    public string? Reason { get; set; }
    public Guid? ApprovedBy { get; set; }
}
