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
        var st = transfer.Status ?? "";
        if (!st.Equals("delivery_staff_assigned", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Transfer must have delivery staff assigned before shipping can be confirmed.");

        var role = request.ActingRole?.Trim();
        var isAdmin = string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
        if (!isAdmin)
        {
            if (!request.ActingUserId.HasValue)
                throw new ValidationException("User context is required to confirm dispatch.");

            var assignees = transfer.DeliveryStaffIds ?? new List<Guid>();
            if (!assignees.Contains(request.ActingUserId.Value))
                throw new ValidationException("Only fulfillment staff assigned to this transfer can confirm dispatch.");

            var actor = await _context.UserAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.ActingUserId.Value, cancellationToken)
                ?? throw new ValidationException("User not found.");

            if (!string.Equals(actor.Role, "fulfillment_staff", StringComparison.OrdinalIgnoreCase))
                throw new ValidationException("Only fulfillment staff (or admin) can confirm dispatch.");

            var onSourceBranch = await _context.StaffAssignments
                .AsNoTracking()
                .AnyAsync(
                    sa => sa.StaffId == request.ActingUserId.Value && sa.BranchId == transfer.FromBranchId,
                    cancellationToken);
            if (!onSourceBranch)
                throw new ValidationException("You must be assigned to the originating branch to confirm this shipment.");
        }

        // Deduct from Source Reserved Stock
        // Prioritize "Sales" or "Storefront" locations
        var stocks = await _context.BatchStocks
            .Include(bs => bs.Location)
            .Where(bs => bs.BatchId == transfer.BatchId && bs.Location != null && bs.Location.BranchId == transfer.FromBranchId)
            .ToListAsync(cancellationToken);

        var sourceStock = stocks.FirstOrDefault(s => s.Location?.Type == "Sales" || s.Location?.Type == "Storefront") 
                          ?? stocks.FirstOrDefault();

        int? sourceStockSnapshot = null;

        if (sourceStock != null && sourceStock.Quantities != null)
        {
            var root = sourceStock.Quantities.RootElement;
            var quantities = JsonSerializer.Deserialize<BatchStockQuantities>(root.GetRawText());
            if (quantities != null)
            {
                sourceStockSnapshot = quantities.AvailableQuantity;
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

        // Take a fresh snapshot of the destination stock before shipping (to record its baseline)
        var destStockSnapshot = 0;
        var destBatchId = transfer.BatchId;
        // Try to find if a listing already exists for this taxonomy at the destination to get the primary batch
        var existingDestListing = await _context.ProductListings
            .Include(pl => pl.Batch)
            .FirstOrDefaultAsync(pl => pl.BranchId == transfer.ToBranchId && pl.Batch != null && pl.Batch.TaxonomyId == (transfer.Batch != null ? transfer.Batch.TaxonomyId : Guid.Empty), cancellationToken);
        
        var effectiveDestBatchId = existingDestListing?.BatchId ?? destBatchId;
        var destBatchStock = await _context.BatchStocks
            .Where(bs => bs.BatchId == effectiveDestBatchId)
            .Where(bs => _context.InventoryLocations.Any(l => l.Id == bs.LocationId && l.BranchId == transfer.ToBranchId))
            .FirstOrDefaultAsync(cancellationToken);
        
        if (destBatchStock != null && destBatchStock.Quantities != null)
        {
            var dq = JsonSerializer.Deserialize<BatchStockQuantities>(destBatchStock.Quantities.RootElement.GetRawText());
            destStockSnapshot = dq?.AvailableQuantity ?? 0;
        }

        // Update Transfer
        transfer.Status = "shipped";
        
        // Update Logistics Info with fresh snapshots
        transfer.LogisticsInfo = InventoryMapper.BuildLogisticsInfo(
            shippedAt: DateTime.UtcNow,
            shippedBy: request.ShippedBy,
            trackingNumber: request.TrackingNumber,
            shippingProvider: request.ShippingProvider,
            fromStockSnapshot: sourceStockSnapshot,
            toStockSnapshot: destStockSnapshot,
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
