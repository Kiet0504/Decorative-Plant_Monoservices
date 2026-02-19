using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class RequestStockTransferCommandHandler : IRequestHandler<RequestStockTransferCommand, StockTransferDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public RequestStockTransferCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<StockTransferDto> Handle(RequestStockTransferCommand request, CancellationToken cancellationToken)
    {
        // Validate stock availability
        var stockRepo = _repositoryFactory.CreateRepository<BatchStock>();
        var sourceStock = await stockRepo.FirstOrDefaultAsync(
            s => s.BatchId == request.BatchId && s.LocationId == request.FromLocationId,
            cancellationToken
        );

        if (sourceStock == null)
            throw new ValidationException("Source stock not found.");

        var quantities = sourceStock.Quantities != null 
            ? JsonSerializer.Deserialize<BatchStockQuantities>(sourceStock.Quantities) 
            : null;

        if (quantities == null || quantities.AvailableQuantity < request.Quantity)
             throw new ValidationException("Insufficient available stock for transfer.");

        // Create Transfer Request
        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(),
            TransferCode = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
            BatchId = request.BatchId,
            FromBranchId = request.FromBranchId,
            ToBranchId = request.ToBranchId,
            FromLocationId = request.FromLocationId,
            ToLocationId = request.ToLocationId, // Might be null/TBD initially, but mandated here for simplicity ?? No, likely target location known or TBD. 
            // If TBD, should be nullable. DTO says Guid (non-nullable). Let's assume mandatory for now logic.
            Quantity = request.Quantity,
            Status = "Requested",
            CreatedAt = DateTime.UtcNow,
            LogisticsInfo = InventoryMapper.BuildLogisticsInfo(
                requestedBy: request.RequestedBy,
                notes: request.Notes
            )
        };

        var transferRepo = _repositoryFactory.CreateRepository<StockTransfer>();
        await transferRepo.AddAsync(transfer, cancellationToken);
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
