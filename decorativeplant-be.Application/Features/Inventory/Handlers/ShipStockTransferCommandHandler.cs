using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ShipStockTransferCommandHandler : IRequestHandler<ShipStockTransferCommand, StockTransferDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public ShipStockTransferCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<StockTransferDto> Handle(ShipStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transferRepo = _repositoryFactory.CreateRepository<StockTransfer>();
        var transfer = await transferRepo.GetByIdAsync(request.TransferId, cancellationToken);
        
        if (transfer == null) throw new NotFoundException(nameof(StockTransfer), request.TransferId);
        if (transfer.Status != "Approved") throw new ValidationException("Transfer must be Approved before Shipping.");

        // Deduct from Source Reserved Stock
        var stockRepo = _repositoryFactory.CreateRepository<BatchStock>();
        var sourceStock = await stockRepo.FirstOrDefaultAsync(s => s.BatchId == transfer.BatchId && s.LocationId == transfer.FromLocationId, cancellationToken);

        if (sourceStock != null && sourceStock.Quantities != null)
        {
            var quantities = JsonSerializer.Deserialize<BatchStockQuantities>(sourceStock.Quantities);
            if (quantities != null)
            {
                // We reserved it, so deduct from Reserved and Total
                quantities.ReservedQuantity -= transfer.Quantity;
                quantities.Quantity -= transfer.Quantity;
                // Available remains same (already deducted during Approve)
                
                sourceStock.Quantities = JsonSerializer.SerializeToDocument(quantities);
                await stockRepo.UpdateAsync(sourceStock, cancellationToken);
            }
        }

        // Update Transfer
        transfer.Status = "Shipped";
        
        // Update Logistics Info
        transfer.LogisticsInfo = InventoryMapper.BuildLogisticsInfo(
            shippedAt: DateTime.UtcNow,
            shippedBy: request.ShippedBy,
            trackingNumber: request.TrackingNumber,
            shippingProvider: request.ShippingProvider,
            existingInfo: transfer.LogisticsInfo
        );

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return InventoryMapper.ToStockTransferDto(transfer);
    }

    private class BatchStockQuantities
    {
        [System.Text.Json.Serialization.JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("reserved_quantity")]
        public int ReservedQuantity { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("available_quantity")]
        public int AvailableQuantity { get; set; }
    }
}
