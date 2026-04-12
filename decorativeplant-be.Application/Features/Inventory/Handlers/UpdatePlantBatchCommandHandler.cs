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

        if (request.InitialQuantity.HasValue)
            entity.InitialQuantity = request.InitialQuantity;

        // 1. Sync Quantities to BatchStock
        if (request.CurrentTotalQuantity.HasValue)
        {
            entity.CurrentTotalQuantity = request.CurrentTotalQuantity;
            
            if (entity.BatchStocks != null)
            {
                foreach (var bs in entity.BatchStocks)
                {
                    if (bs.Quantities != null)
                    {
                        // Update available_quantity in JSONB
                        var jsonStr = bs.Quantities.RootElement.GetRawText();
                        var quantities = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonStr) ?? new();
                        quantities["available_quantity"] = request.CurrentTotalQuantity.Value;
                        bs.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(quantities));
                    }
                }
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
