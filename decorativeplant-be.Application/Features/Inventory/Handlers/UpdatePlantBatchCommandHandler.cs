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
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;

    public UpdatePlantBatchCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork, IApplicationDbContext context)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<PlantBatchDto> Handle(UpdatePlantBatchCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.PlantBatches
            .Include(b => b.BatchStocks)
            .Include(b => b.ProductListings)
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
            
        if (request.CurrentTotalQuantity.HasValue)
        {
            entity.CurrentTotalQuantity = request.CurrentTotalQuantity;
            
            // --- SYNC: Update available quantity in all associated BatchStock records ---
            var batchStockRepo = _repositoryFactory.CreateRepository<BatchStock>();
            foreach (var bs in entity.BatchStocks)
            {
                if (bs.Quantities != null)
                {
                    var quantities = JsonSerializer.Deserialize<Dictionary<string, object>>(bs.Quantities.RootElement.GetRawText()) ?? new();
                    quantities["available_quantity"] = request.CurrentTotalQuantity.Value;
                    bs.Quantities = JsonDocument.Parse(JsonSerializer.Serialize(quantities));
                    await batchStockRepo.UpdateAsync(bs, cancellationToken);
                }
            }
        }

        if (!string.IsNullOrEmpty(request.Price))
        {
            // --- SYNC: Update price in all associated ProductListing records ---
            var productRepo = _repositoryFactory.CreateRepository<ProductListing>();
            foreach (var p in entity.ProductListings)
            {
                if (p.ProductInfo != null)
                {
                    var productInfo = JsonSerializer.Deserialize<Dictionary<string, object>>(p.ProductInfo.RootElement.GetRawText()) ?? new();
                    productInfo["price"] = request.Price;
                    p.ProductInfo = JsonDocument.Parse(JsonSerializer.Serialize(productInfo));
                    await productRepo.UpdateAsync(p, cancellationToken);
                }
            }
        }

        if (request.SourceInfo != null)
            entity.SourceInfo = PlantBatchMapper.BuildJson(request.SourceInfo);
            
        if (request.Specs != null)
            entity.Specs = PlantBatchMapper.BuildJson(request.Specs);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch needed relations for DTO display
        if (entity.TaxonomyId.HasValue)
        {
             var taxRepo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
             entity.Taxonomy = await taxRepo.GetByIdAsync(entity.TaxonomyId.Value, cancellationToken);
        }

        if (entity.BranchId.HasValue)
        {
             var branchRepo = _repositoryFactory.CreateRepository<decorativeplant_be.Domain.Entities.Branch>();
             entity.Branch = await branchRepo.GetByIdAsync(entity.BranchId.Value, cancellationToken);
        }

        return PlantBatchMapper.ToDto(entity);
    }
}
