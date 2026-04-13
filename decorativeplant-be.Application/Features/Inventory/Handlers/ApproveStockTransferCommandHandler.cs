using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

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
        var transfer = await _context.StockTransfers.FirstOrDefaultAsync(t => t.Id == request.TransferId, cancellationToken);

        if (transfer == null)
            throw new NotFoundException(nameof(StockTransfer), request.TransferId);

        if (transfer.Status == null || !transfer.Status.Equals("requested", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Transfer is not in Requested state.");

        transfer.Status = request.Approved ? "approved" : "rejected";
        
        if (request.Approved)
        {
            if (request.FromBranchId.HasValue)
            {
                transfer.FromBranchId = request.FromBranchId.Value;
            }

            if (!transfer.FromBranchId.HasValue)
            {
                throw new ValidationException("A source branch (FromBranchId) must be specified to approve this transfer.");
            }

            // Deduct stock from the source branch
            // For now, if we don't have an exact BatchId, we just find any stock in the FromBranchId 
            // In a real system we'd deduct based on Product ID matched via listing
            // For simplicity (MVP), we assume there's a way. Let's try to grab a BatchStock in FromBranchId
            var stockToDeduct = await _context.BatchStocks
                .Include(bs => bs.Location)
                .Where(bs => bs.Location != null && bs.Location.BranchId == transfer.FromBranchId)
                // In full implementation, we filter by BatchId or Product matching Listing
                .FirstOrDefaultAsync(cancellationToken);

            if (stockToDeduct != null && stockToDeduct.Quantities != null)
            {
                var root = stockToDeduct.Quantities.RootElement;
                int total = root.TryGetProperty("quantity", out var t) ? t.GetInt32() : 0;
                int reserved = root.TryGetProperty("reserved_quantity", out var r) ? r.GetInt32() : 0;
                int available = root.TryGetProperty("available_quantity", out var a) ? a.GetInt32() : 0;

                if (available < transfer.Quantity)
                    throw new ValidationException("Insufficient available stock in the selected branch to approve transfer.");

                available -= transfer.Quantity;
                reserved += transfer.Quantity;

                stockToDeduct.Quantities = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new
                {
                    quantity = total,
                    reserved_quantity = reserved,
                    available_quantity = available
                }));
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
                }
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return InventoryMapper.ToStockTransferDto(transfer);
    }
}
