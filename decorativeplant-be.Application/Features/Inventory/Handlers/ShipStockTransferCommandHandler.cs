using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Collections.Generic;
using decorativeplant_be.Application.Features.Inventory;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ShipStockTransferCommandHandler : IRequestHandler<ShipStockTransferCommand, StockTransferDto>
{
    private readonly IApplicationDbContext _context;
    private readonly Microsoft.Extensions.Logging.ILogger<ShipStockTransferCommandHandler> _logger;

    public ShipStockTransferCommandHandler(IApplicationDbContext context, Microsoft.Extensions.Logging.ILogger<ShipStockTransferCommandHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<StockTransferDto> Handle(ShipStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await _context.StockTransfers
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .FirstOrDefaultAsync(t => t.Id == request.TransferId, cancellationToken);
        
        if (transfer == null) throw new NotFoundException(nameof(StockTransfer), request.TransferId);
        if (transfer.Status != "approved") throw new ValidationException("Transfer must be Approved before Shipping.");

        // Deduct from Source Reserved Stock
        // Prioritize "Sales" or "Storefront" locations
        var stocks = await _context.BatchStocks
            .Include(bs => bs.Location)
            .Where(bs => bs.BatchId == transfer.BatchId && bs.Location != null && bs.Location.BranchId == transfer.FromBranchId)
            .ToListAsync(cancellationToken);

        var sourceStock = stocks.FirstOrDefault(s => s.Location?.Type == "Sales" || s.Location?.Type == "Storefront") 
                          ?? stocks.FirstOrDefault();

        if (sourceStock != null && sourceStock.Quantities != null)
        {
            var root = sourceStock.Quantities.RootElement;
            var quantities = JsonSerializer.Deserialize<BatchStockQuantities>(root.GetRawText());
            if (quantities != null)
            {
                // In this workflow, we deduct directly from Available, Total, and TotalReceived
                if (quantities.AvailableQuantity < transfer.Quantity)
                {
                    throw new ValidationException($"Insufficient available stock ({quantities.AvailableQuantity}) at Source Branch to ship {transfer.Quantity} units.");
                }

                quantities.AvailableQuantity -= transfer.Quantity;
                quantities.Quantity -= transfer.Quantity;
                quantities.TotalReceived -= transfer.Quantity;
                // reserved_quantity remains untouched (backstock)
                
                sourceStock.Quantities = JsonSerializer.SerializeToDocument(quantities);
                sourceStock.UpdatedAt = DateTime.UtcNow;

                // Sync global batch total
                if (transfer.Batch != null)
                {
                    transfer.Batch.CurrentTotalQuantity = (transfer.Batch.CurrentTotalQuantity ?? 0) - transfer.Quantity;
                }

                // Sync ProductListing for the source branch (Available quantity reflects what's on the web)
                var listing = await _context.ProductListings
                    .FirstOrDefaultAsync(pl => pl.BatchId == transfer.BatchId && pl.BranchId == transfer.FromBranchId, cancellationToken);
                
                if (listing != null && listing.ProductInfo != null)
                {
                    var productInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(listing.ProductInfo.RootElement.GetRawText());
                    if (productInfo != null)
                    {
                        productInfo["stock_quantity"] = quantities.AvailableQuantity;
                        listing.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(productInfo));
                    }
                }
            }
        }

        // Update Transfer
        transfer.Status = "shipped";
        
        // Update Logistics Info
        transfer.LogisticsInfo = InventoryMapper.BuildLogisticsInfo(
            shippedAt: DateTime.UtcNow,
            shippedBy: request.ShippedBy,
            trackingNumber: request.TrackingNumber,
            shippingProvider: request.ShippingProvider,
            existingInfo: transfer.LogisticsInfo
        );

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
