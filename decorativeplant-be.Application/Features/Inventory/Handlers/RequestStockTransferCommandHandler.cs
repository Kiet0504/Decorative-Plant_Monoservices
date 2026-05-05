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
        int fromSnapshot = 0;
        int toSnapshot = 0;
        Guid? resolvedBatchId = request.BatchId;

        // === PATH 1: Validate via BatchStock (standard path when batch exists) ===
        if (request.BatchId.HasValue && request.BatchId.Value != Guid.Empty)
        {
            var stockRepo = _repositoryFactory.CreateRepository<BatchStock>();
            var sourceStock = await stockRepo.FirstOrDefaultAsync(
                s => s.BatchId == request.BatchId && s.LocationId == request.FromLocationId,
                cancellationToken
            );

            if (sourceStock != null)
            {
                var quantities = sourceStock.Quantities != null 
                    ? JsonSerializer.Deserialize<BatchStockQuantities>(sourceStock.Quantities) 
                    : null;

                if (quantities == null || quantities.AvailableQuantity < request.Quantity)
                    throw new ValidationException("Insufficient available stock for transfer.");

                fromSnapshot = quantities.AvailableQuantity;

                // Fetch destination branch stock snapshot
                var destStock = await stockRepo.FirstOrDefaultAsync(
                    s => s.BatchId == request.BatchId && s.LocationId == request.ToLocationId,
                    cancellationToken
                );
                if (destStock?.Quantities != null)
                {
                    var destQuantities = JsonSerializer.Deserialize<BatchStockQuantities>(destStock.Quantities);
                    toSnapshot = destQuantities?.AvailableQuantity ?? 0;
                }
            }
            else
            {
                // BatchStock not found — batch may have been deleted. Fall through to listing path.
                resolvedBatchId = null;
            }
        }
        else
        {
            resolvedBatchId = null;
        }

        // === PATH 2: Validate via ProductListing (fallback when batch was deleted) ===
        if (!resolvedBatchId.HasValue || resolvedBatchId.Value == Guid.Empty)
        {
            if (!request.ListingId.HasValue || request.ListingId.Value == Guid.Empty)
                throw new ValidationException("No batch or product listing ID provided for stock validation.");

            var listingRepo = _repositoryFactory.CreateRepository<ProductListing>();
            var listing = await listingRepo.FirstOrDefaultAsync(
                l => l.Id == request.ListingId.Value && l.BranchId == request.FromBranchId,
                cancellationToken
            );

            if (listing == null)
                throw new ValidationException("Source product listing not found at the specified branch.");

            // Extract stock_quantity from ProductInfo JSON
            int listingStock = 0;
            if (listing.ProductInfo != null)
            {
                var root = listing.ProductInfo.RootElement;
                if (root.TryGetProperty("stock_quantity", out var sq))
                    listingStock = sq.GetInt32();
            }

            if (listingStock < request.Quantity)
                throw new ValidationException($"Insufficient storefront stock for transfer. Available: {listingStock}, Requested: {request.Quantity}");

            fromSnapshot = listingStock;

            // Use the listing's batch ID if it has one, otherwise keep null
            resolvedBatchId = listing.BatchId;
        }

        // Create Transfer Request
        var transfer = new StockTransfer
        {
            Id = Guid.NewGuid(),
            TransferCode = $"TRF-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
            BatchId = resolvedBatchId,
            FromBranchId = request.FromBranchId,
            ToBranchId = request.ToBranchId,
            FromLocationId = request.FromLocationId,
            ToLocationId = request.ToLocationId,
            Quantity = request.Quantity,
            Status = "Requested",
            CreatedAt = DateTime.UtcNow,
            LogisticsInfo = InventoryMapper.BuildLogisticsInfo(
                requestedBy: request.RequestedBy,
                notes: request.Notes,
                fromStockSnapshot: fromSnapshot,
                toStockSnapshot: toSnapshot
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

