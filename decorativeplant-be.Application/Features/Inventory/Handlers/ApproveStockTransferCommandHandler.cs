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
            // In this workflow, stock is NOT deducted during approval.
            // It is only deducted when the source branch clicks "Ship".
            // Here we only ensure the source branch is correctly identified.
            if (request.FromBranchId.HasValue)
            {
                transfer.FromBranchId = request.FromBranchId.Value;
            }

            if (!transfer.FromBranchId.HasValue)
            {
                throw new ValidationException("A source branch (FromBranchId) must be specified to approve this transfer.");
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
