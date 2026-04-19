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
            .Include(b => b.ParentBatch) // Fix: Include parent batch to preserve lineage info in response
            .FirstOrDefaultAsync(b => b.Id == request.Id, cancellationToken);

        if (entity == null)
        {
            throw new NotFoundException(nameof(PlantBatch), request.Id);
        }

        if (request.BatchCode != null)
            entity.BatchCode = request.BatchCode;

        // Validation: Block stage regression if plants have been sent to sales
        if (request.Specs != null && request.Specs.TryGetValue("maturity_stage", out var newStageObj))
        {
            var newStage = newStageObj?.ToString()?.ToLower();
            var oldStage = string.Empty;
            if (entity.Specs != null && entity.Specs.RootElement.TryGetProperty("maturity_stage", out var oldStageProp))
            {
                oldStage = oldStageProp.GetString()?.ToLower();
            }

            if (!string.IsNullOrEmpty(newStage) && !string.IsNullOrEmpty(oldStage) && newStage != oldStage)
            {
                int newLevel = GetStageLevel(newStage);
                int oldLevel = GetStageLevel(oldStage);

                if (newLevel < oldLevel)
                {
                    // Check if any plants were already sent to sales
                    int totalPublished = 0;
                    if (entity.BatchStocks != null)
                    {
                        foreach (var bs in entity.BatchStocks)
                        {
                            if (bs.Quantities != null && bs.Quantities.RootElement.TryGetProperty("total_received", out var trProp))
                            {
                                totalPublished += trProp.GetInt32();
                            }
                        }
                    }

                    if (totalPublished > 0)
                    {
                        throw new BadRequestException($"Cannot regress stage from '{oldStage}' to '{newStage}' because {totalPublished} plants have already been sent to sales. This batch is now locked to its commercial maturity level.");
                    }
                }
            }
        }
            
        if (request.BranchId.HasValue)
            entity.BranchId = request.BranchId;
            
        if (request.TaxonomyId.HasValue)
            entity.TaxonomyId = request.TaxonomyId;

        if (request.SupplierId.HasValue)
            entity.SupplierId = request.SupplierId;

        if (request.ParentBatchId.HasValue && request.ParentBatchId.Value != entity.Id)
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
                
                // Determine the limit (Total Received) - Only apply strict validation for Sales/Storefront
                bool isSalesLocation = bs.Location?.Type == "Sales" || bs.Location?.Type == "Storefront";
                
                // 1. Universal field updates (Always sync if provided)
                if (request.ReservedQuantity.HasValue) quantities["reserved_quantity"] = request.ReservedQuantity.Value;
                if (request.Quantity.HasValue) quantities["quantity"] = request.Quantity.Value;
                if (!isSalesLocation && request.AvailableQuantity.HasValue) quantities["available_quantity"] = request.AvailableQuantity.Value;

                if (isSalesLocation)
                {
                        // Only validate/update available_quantity if specifically requested or if targeting a branch
                        if (request.AvailableQuantity.HasValue || (request.BranchId.HasValue && request.CurrentTotalQuantity.HasValue))
                        {
                            int limit = 0;
                            if (quantities.TryGetValue("total_received", out var tr)) limit = tr;
                            else if (quantities.TryGetValue("quantity", out var q)) limit = q; 
                            else limit = int.MaxValue; 

                            int targetAvailable = request.AvailableQuantity ?? request.CurrentTotalQuantity ?? 0;

                            if (targetAvailable > limit)
                            {
                                throw new BadRequestException($"Cannot update sales stock to {targetAvailable}. Maximum available from cultivation is {limit}.");
                            }
                            
                            quantities["available_quantity"] = targetAvailable;

                            // Sync the 'quantity' field for sales site to match available if reserved is 0
                            if (quantities.TryGetValue("reserved_quantity", out var res) && res == 0)
                            {
                                quantities["quantity"] = targetAvailable;
                            }
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

        if (request.SourceInfo != null || request.PurchaseCost.HasValue)
        {
            var sourceInfo = request.SourceInfo ?? new Dictionary<string, object>();
            if (request.SourceInfo == null && entity.SourceInfo != null) 
            {
               try { sourceInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.SourceInfo.RootElement.GetRawText()) ?? new(); } catch {}
            }
            if (request.PurchaseCost.HasValue)
            {
                sourceInfo["purchase_cost"] = request.PurchaseCost.Value;
            }
            entity.SourceInfo = PlantBatchMapper.BuildJson(sourceInfo);
        }

            
        if (request.Specs != null)
            entity.Specs = PlantBatchMapper.BuildJson(request.Specs);

        await _context.SaveChangesAsync(cancellationToken);

        return PlantBatchMapper.ToDto(entity);
    }

    private static int GetStageLevel(string stage)
    {
        return stage switch
        {
            "seedling" => 0,
            "juvenile" => 1,
            "mature" => 2,
            "flowering" => 3,
            "stable" => 4,
            _ => 100 // Unknown stages are considered "high"
        };
    }
}
