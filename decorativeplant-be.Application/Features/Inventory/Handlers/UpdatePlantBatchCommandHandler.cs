using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class UpdatePlantBatchCommandHandler : IRequestHandler<UpdatePlantBatchCommand, PlantBatchDto>
{
    private readonly IApplicationDbContext _context;

    public UpdatePlantBatchCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PlantBatchDto> Handle(UpdatePlantBatchCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.PlantBatches
            .Include(b => b.BatchStocks)
                .ThenInclude(bs => bs.Location)
            .Include(b => b.ProductListings)
            .Include(b => b.Taxonomy)
            .Include(b => b.Branch)
            .Include(b => b.Supplier)
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (entity == null)
        {
            throw new NotFoundException(nameof(PlantBatch), request.Id);
        }

        if (request.BatchCode != null)
            entity.BatchCode = request.BatchCode;
            
        if (request.BranchId.HasValue)
            entity.BranchId = request.BranchId;
            
        if (request.TaxonomyId.HasValue)
            entity.TaxonomyId = request.TaxonomyId;

        if (request.SupplierId.HasValue)
            entity.SupplierId = request.SupplierId;

        if (request.ParentBatchId.HasValue)
            entity.ParentBatchId = request.ParentBatchId;
            
        // 1. Sync Quantities to BatchStock with Validation
        if (request.CurrentTotalQuantity.HasValue && entity.BatchStocks != null)
        {
            // Filter stocks to update: if BranchId is provided, only update that branch's sales stock
            var stocksToUpdate = request.BranchId.HasValue 
                ? entity.BatchStocks.Where(bs => bs.Location?.BranchId == request.BranchId && 
                                               (bs.Location?.Type == "Sales" || bs.Location?.Type == "Storefront"))
                : entity.BatchStocks;

            foreach (var bs in stocksToUpdate)
            {
                if (bs.Quantities == null) continue;

                var jsonStr = bs.Quantities.RootElement.GetRawText();
                var quantities = JsonSerializer.Deserialize<Dictionary<string, int>>(jsonStr) ?? new();
                
                // Determine the limit (Total Received)
                int limit = 0;
                if (quantities.TryGetValue("total_received", out var tr)) limit = tr;
                else if (quantities.TryGetValue("quantity", out var q)) limit = q; // Fallback for old data
                else limit = int.MaxValue; 

                if (request.CurrentTotalQuantity.Value > limit)
                {
                    throw new BadRequestException($"Cannot update stock to {request.CurrentTotalQuantity.Value}. Maximum available from cultivation is {limit}.");
                }

                // Update available_quantity
                quantities["available_quantity"] = request.CurrentTotalQuantity.Value;
                
                // If this is a Sales location (reserved is usually 0), sync the 'quantity' as well
                if (bs.Location?.Type == "Sales" || bs.Location?.Type == "Storefront")
                {
                    if (quantities.TryGetValue("reserved_quantity", out var res) && res == 0)
                    {
                        quantities["quantity"] = request.CurrentTotalQuantity.Value;
                    }
                }

                bs.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(quantities));
            }
            
            // Update the master total in PlantBatch only if we updating globally
            if (!request.BranchId.HasValue)
            {
                 entity.CurrentTotalQuantity = request.CurrentTotalQuantity;
            }
        }

        // 2. Sync Price to ProductListing
        if (!string.IsNullOrEmpty(request.Price) && entity.ProductListings != null)
        {
            foreach (var pl in entity.ProductListings)
            {
                if (pl.ProductInfo != null)
                {
                    // Update price in JSONB
                    var jsonStr = pl.ProductInfo.RootElement.GetRawText();
                    var productInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonStr) ?? new();
                    productInfo["price"] = request.Price;
                    pl.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(productInfo));
                }
            }
        }

        if (request.SourceInfo != null)
            entity.SourceInfo = PlantBatchMapper.BuildJson(request.SourceInfo);
            
        if (request.Specs != null)
            entity.Specs = PlantBatchMapper.BuildJson(request.Specs);

        await _context.SaveChangesAsync(cancellationToken);

        return PlantBatchMapper.ToDto(entity);
    }
}
