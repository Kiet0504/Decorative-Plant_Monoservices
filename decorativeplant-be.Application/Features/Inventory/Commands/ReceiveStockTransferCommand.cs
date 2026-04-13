using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class ReceiveStockTransferCommand : IRequest<StockTransferDto>
{
    public Guid TransferId { get; set; }
    public string? ReceivedBy { get; set; }
    public string? Notes { get; set; }
}
