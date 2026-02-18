using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ReceiveStockTransferCommandHandler : IRequestHandler<ReceiveStockTransferCommand, StockTransferDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public ReceiveStockTransferCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<StockTransferDto> Handle(ReceiveStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transferRepo = _repositoryFactory.CreateRepository<StockTransfer>();
        var transfer = await transferRepo.GetByIdAsync(request.TransferId, cancellationToken);

        if (transfer == null) throw new NotFoundException(nameof(StockTransfer), request.TransferId);
        if (transfer.Status != "Shipped") throw new ValidationException("Transfer must be Shipped before Receiving.");

        // Add to Destination Stock
        var stockRepo = _repositoryFactory.CreateRepository<BatchStock>();
        var destStock = await stockRepo.FirstOrDefaultAsync(s => s.BatchId == transfer.BatchId && s.LocationId == transfer.ToLocationId, cancellationToken);

        if (destStock == null)
        {
            destStock = new BatchStock
            {
                Id = Guid.NewGuid(),
                BatchId = transfer.BatchId,
                LocationId = transfer.ToLocationId,
                Quantities = JsonSerializer.SerializeToDocument(new BatchStockQuantities { Quantity = 0, ReservedQuantity = 0, AvailableQuantity = 0 }),
                UpdatedAt = DateTime.UtcNow
            };
            await stockRepo.AddAsync(destStock, cancellationToken);
        }

        var quantities = destStock.Quantities != null 
            ? JsonSerializer.Deserialize<BatchStockQuantities>(destStock.Quantities) 
            : new BatchStockQuantities();
            
        if (quantities == null) quantities = new BatchStockQuantities();

        // Add received stock
        quantities.Quantity += transfer.Quantity;
        quantities.AvailableQuantity += transfer.Quantity;
        // Reserved remains same

        destStock.Quantities = JsonSerializer.SerializeToDocument(quantities);
        destStock.UpdatedAt = DateTime.UtcNow;
        if (await stockRepo.ExistsAsync(s => s.Id == destStock.Id, cancellationToken))
             await stockRepo.UpdateAsync(destStock, cancellationToken);

        // Update Transfer
        transfer.Status = "Received";
        
        // Update Logistics Info
        transfer.LogisticsInfo = InventoryMapper.BuildLogisticsInfo(
            receivedAt: DateTime.UtcNow,
            receivedBy: request.ReceivedBy,
            receivingNotes: request.Notes,
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
