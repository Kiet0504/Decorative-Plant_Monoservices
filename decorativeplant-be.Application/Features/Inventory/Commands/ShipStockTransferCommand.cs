using decorativeplant_be.Application.Features.Inventory.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Commands;

public class ShipStockTransferCommand : IRequest<StockTransferDto>
{
    public Guid TransferId { get; set; }
    public string? ShippingProvider { get; set; }
    public string? TrackingNumber { get; set; }
    public Guid? ShippedBy { get; set; }
}
