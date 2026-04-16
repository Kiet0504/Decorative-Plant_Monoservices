using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Commerce.Orders;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Collections.Generic;
using decorativeplant_be.Application.Features.Inventory;
using Microsoft.Extensions.Logging;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ReceiveStockTransferCommandHandler : IRequestHandler<ReceiveStockTransferCommand, StockTransferDto>
{
    private readonly IApplicationDbContext _context;
    private readonly Microsoft.Extensions.Logging.ILogger<ReceiveStockTransferCommandHandler> _logger;

    public ReceiveStockTransferCommandHandler(IApplicationDbContext context, Microsoft.Extensions.Logging.ILogger<ReceiveStockTransferCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<StockTransferDto> Handle(ReceiveStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await _context.StockTransfers
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .FirstOrDefaultAsync(t => t.Id == request.TransferId, cancellationToken);

        if (transfer == null) throw new NotFoundException(nameof(StockTransfer), request.TransferId);
        if (transfer.Status != "shipped") throw new ValidationException("Transfer must be Shipped before Receiving.");

        // Identify Target Location
        var targetLocationId = transfer.ToLocationId;
        if (targetLocationId == null)
        {
            // Prioritize "Sales" or "Storefront" locations
            var salesLoc = await _context.InventoryLocations
                .Where(l => l.BranchId == transfer.ToBranchId && (l.Type == "Sales" || l.Type == "Storefront"))
                .OrderBy(l => l.Name)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (salesLoc == null)
            {
                // Create a default "Sales" location if none exists
                salesLoc = new InventoryLocation
                {
                    Id = Guid.NewGuid(),
                    BranchId = transfer.ToBranchId,
                    Name = "Main Storefront",
                    Type = "Sales"
                };
                _context.InventoryLocations.Add(salesLoc);
            }
            targetLocationId = salesLoc.Id;
        }

        // CONSOLIDATION LOGIC: Check if this branch already has a listing for this exact species (Taxonomy)
        // If it does, we merge the stock into that existing listing's primary batch to avoid duplicates.
        var effectiveBatchId = transfer.BatchId!.Value;
        var listing = await _context.ProductListings
            .Include(pl => pl.Batch)
            .FirstOrDefaultAsync(pl => pl.BranchId == transfer.ToBranchId && pl.Batch != null && pl.Batch.TaxonomyId == (transfer.Batch != null ? transfer.Batch.TaxonomyId : Guid.Empty), cancellationToken);

        if (listing != null)
        {
            _logger.LogInformation("Consolidating incoming batch {SourceBatchId} into existing branch batch {TargetBatchId} for taxonomy {TaxonomyId}", 
                transfer.BatchId ?? Guid.Empty, listing.BatchId ?? Guid.Empty, transfer.Batch?.TaxonomyId);
            effectiveBatchId = listing.BatchId ?? Guid.Empty;
        }

        // Add to Destination Stock (using effectiveBatchId which might be the merged batch)
        var destStock = await _context.BatchStocks
            .FirstOrDefaultAsync(s => s.BatchId == effectiveBatchId && s.LocationId == targetLocationId, cancellationToken);

        if (destStock == null)
        {
            destStock = new BatchStock
            {
                Id = Guid.NewGuid(),
                BatchId = effectiveBatchId,
                LocationId = targetLocationId,
                Quantities = JsonSerializer.SerializeToDocument(new BatchStockQuantities { Quantity = 0, ReservedQuantity = 0, AvailableQuantity = 0, TotalReceived = 0 }),
                UpdatedAt = DateTime.UtcNow
            };
            _context.BatchStocks.Add(destStock);
        }

        var quantities = JsonSerializer.Deserialize<BatchStockQuantities>(destStock.Quantities!.RootElement.GetRawText()) 
            ?? new BatchStockQuantities();

        // Add received stock to all indicators
        quantities.Quantity += transfer.Quantity;
        quantities.AvailableQuantity += transfer.Quantity;
        quantities.TotalReceived += transfer.Quantity; 

        destStock.Quantities = JsonSerializer.SerializeToDocument(quantities);
        destStock.UpdatedAt = DateTime.UtcNow;

        // SYNC targeted batch total (global)
        var targetBatch = (effectiveBatchId == transfer.BatchId) 
            ? transfer.Batch 
            : await _context.PlantBatches.FirstOrDefaultAsync(b => b.Id == effectiveBatchId, cancellationToken);

        if (targetBatch != null)
        {
            targetBatch.CurrentTotalQuantity = (targetBatch.CurrentTotalQuantity ?? 0) + transfer.Quantity;
        }

        // Update Transfer
        transfer.Status = "received";

        // Advance linked BOPIS order: stock_transferring → ready_for_pickup.
        // Non-BOPIS transfers (plain replenishment) have no linked order — skip.
        if (transfer.LogisticsInfo != null
            && transfer.LogisticsInfo.RootElement.TryGetProperty("order_id", out var orderIdEl)
            && orderIdEl.TryGetGuid(out Guid linkedOrderId))
        {
            var linkedOrder = await _context.OrderHeaders
                .FirstOrDefaultAsync(o => o.Id == linkedOrderId, cancellationToken);
            if (linkedOrder != null
                && OrderStatusMachine.IsBopis(linkedOrder.Status)
                && linkedOrder.Status == OrderStatusMachine.StockTransferring)
            {
                // request.ReceivedBy is a free-form staff name string, so record it in the
                // source/reason rather than the typed changedBy guid slot.
                OrderStatusMachine.Apply(linkedOrder, OrderStatusMachine.ReadyForPickup,
                    changedBy: null,
                    reason: $"Stock transfer {transfer.TransferCode} received by {request.ReceivedBy ?? "staff"}",
                    source: "StockTransferReceive");
            }
        }
        
        // Update Logistics Info
        transfer.LogisticsInfo = InventoryMapper.BuildLogisticsInfo(
            receivedAt: DateTime.UtcNow,
            receivedBy: request.ReceivedBy,
            receivingNotes: request.Notes,
            existingInfo: transfer.LogisticsInfo
        );

        // CREATE LISTING if it didn't exist (though if listing was null above, we'll create it now)
        if (listing == null)
        {
            // Try to find a reference listing for the same taxonomy to copy metadata
            var refListing = await _context.ProductListings
                .Include(pl => pl.Batch)
                .Where(pl => pl.Batch != null && pl.Batch.TaxonomyId == (transfer.Batch != null ? transfer.Batch.TaxonomyId : Guid.Empty))
                .FirstOrDefaultAsync(cancellationToken);

            listing = new ProductListing
            {
                Id = Guid.NewGuid(),
                BranchId = transfer.ToBranchId,
                BatchId = effectiveBatchId,
                CreatedAt = DateTime.UtcNow,
                ProductInfo = refListing?.ProductInfo,
                StatusInfo = refListing?.StatusInfo ?? JsonSerializer.SerializeToDocument(new { status = "active", visibility = "public" }),
                Images = refListing?.Images,
                SeoInfo = refListing?.SeoInfo
            };
            _context.ProductListings.Add(listing);
        }

        // Update storefront quantity (Available quantity reflects what's on the web)
        if (listing.ProductInfo != null)
        {
            var productInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(listing.ProductInfo.RootElement.GetRawText());
            if (productInfo != null)
            {
                productInfo["stock_quantity"] = quantities.AvailableQuantity;
                listing.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(productInfo));
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

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

        [System.Text.Json.Serialization.JsonPropertyName("total_received")]
        public int TotalReceived { get; set; }
    }
}
