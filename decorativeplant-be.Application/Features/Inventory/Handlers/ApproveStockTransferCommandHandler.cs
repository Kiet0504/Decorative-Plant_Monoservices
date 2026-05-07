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

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class ApproveStockTransferCommandHandler : IRequestHandler<ApproveStockTransferCommand, StockTransferDto>
{
    private readonly IApplicationDbContext _context;

    public ApproveStockTransferCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StockTransferDto> Handle(ApproveStockTransferCommand request, CancellationToken cancellationToken)
    {
        var transfer = await _context.StockTransfers
            .Include(x => x.FromBranch)
            .Include(x => x.ToBranch)
            .Include(x => x.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .FirstOrDefaultAsync(t => t.Id == request.TransferId, cancellationToken);

        if (transfer == null)
            throw new NotFoundException(nameof(StockTransfer), request.TransferId);

        if (transfer.Status == null || !transfer.Status.Equals("requested", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Transfer is not in Requested state.");

        transfer.Status = request.Approved ? "approved" : "rejected";
        
        if (request.Approved)
        {
            var fromBranchId = request.FromBranchId ?? transfer.FromBranchId;
            if (!fromBranchId.HasValue)
            {
                throw new ValidationException("A source branch (FromBranchId) must be specified to approve this transfer.");
            }

            // 1. Check Physical Stock Availability
            var stocks = await _context.BatchStocks
                .Include(bs => bs.Location)
                .Where(bs => bs.BatchId == transfer.BatchId && bs.Location != null && bs.Location.BranchId == fromBranchId.Value)
                .ToListAsync(cancellationToken);

            var sourceStock = stocks.FirstOrDefault(s => s.Location?.Type == "Sales" || s.Location?.Type == "Storefront") 
                              ?? stocks.FirstOrDefault();

            if (sourceStock == null || sourceStock.Quantities == null)
            {
                throw new ValidationException("No stock records found at the selected source branch.");
            }

            var quantities = JsonSerializer.Deserialize<BatchStockQuantities>(sourceStock.Quantities.RootElement.GetRawText());
            
            // 2. Calculate Reserved Stock from other pending transfers
            var otherPendingReserved = await _context.StockTransfers
                .Where(t => t.BatchId == transfer.BatchId 
                            && t.FromBranchId == fromBranchId.Value
                            && t.Id != transfer.Id
                            && (t.Status == "requested" || t.Status == "pending" || t.Status == "approved" || t.Status == "delivery_staff_assigned"))
                .SumAsync(t => (int?)t.Quantity, cancellationToken) ?? 0;

            var netAvailable = (quantities?.AvailableQuantity ?? 0) - otherPendingReserved;

            if (netAvailable < transfer.Quantity)
            {
                throw new ValidationException($"Insufficient net available stock ({netAvailable}) at source branch. (Physical: {quantities?.AvailableQuantity ?? 0}, Reserved for other transfers: {otherPendingReserved}).");
            }

            // In this workflow, stock is NOT deducted during approval.
            if (request.FromBranchId.HasValue)
            {
                transfer.FromBranchId = request.FromBranchId.Value;
            }


            // Tie revenue to the OrderItem
            if (transfer.LogisticsInfo != null)
            {
                var root = transfer.LogisticsInfo.RootElement;
                if (root.TryGetProperty("order_id", out var orderIdElement) && orderIdElement.TryGetGuid(out Guid orderId))
                {
                    // Optionally get the listing_id to target the exact item
                    Guid? listingId = null;
                    if (root.TryGetProperty("listing_id", out var listingIdElement) && listingIdElement.TryGetGuid(out Guid lId))
                    {
                        listingId = lId;
                    }

                    var query = _context.OrderItems.Where(oi => oi.OrderId == orderId);
                    if (listingId.HasValue)
                    {
                        query = query.Where(oi => oi.ListingId == listingId.Value);
                    }

                    var orderItem = await query.FirstOrDefaultAsync(cancellationToken);

                    if (orderItem != null)
                    {
                        // ATTRIBUTE REVENUE to the source branch!
                        orderItem.BranchId = transfer.FromBranchId;
                    }

                    // Advance linked BOPIS order: deposit_paid → stock_transferring.
                    // Idempotent: Apply is a no-op when from == to, and CanTransition blocks
                    // non-BOPIS orders (this transfer could exist for any kind of replenishment).
                    var order = await _context.OrderHeaders
                        .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
                    if (order != null
                        && OrderStatusMachine.IsBopis(order.Status)
                        && order.Status == OrderStatusMachine.DepositPaid)
                    {
                        OrderStatusMachine.Apply(order, OrderStatusMachine.StockTransferring,
                            changedBy: null,
                            reason: $"Stock transfer {transfer.TransferCode} approved",
                            source: "StockTransferApprove");
                    }
                }
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
    }
}
