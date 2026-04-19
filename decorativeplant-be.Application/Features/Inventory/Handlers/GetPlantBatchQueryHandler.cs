using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class GetPlantBatchQueryHandler : IRequestHandler<GetPlantBatchQuery, PlantBatchDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IApplicationDbContext _context;

    public GetPlantBatchQueryHandler(IRepositoryFactory repositoryFactory, IApplicationDbContext context)
    {
        _repositoryFactory = repositoryFactory;
        _context = context;
    }

    public async Task<PlantBatchDto> Handle(GetPlantBatchQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<PlantBatch>();
        var entity = await repo.GetByIdAsync(request.Id, cancellationToken);

        if (entity == null)
        {
            throw new NotFoundException(nameof(PlantBatch), request.Id);
        }

        // Load relations for detailed view
        // 1. Taxonomy
        if (entity.TaxonomyId.HasValue && entity.Taxonomy == null)
        {
            var taxRepo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
            entity.Taxonomy = await taxRepo.GetByIdAsync(entity.TaxonomyId.Value, cancellationToken);
        }
        
        // 2. Supplier
        if (entity.SupplierId.HasValue && entity.Supplier == null)
        {
            var supRepo = _repositoryFactory.CreateRepository<Supplier>();
            entity.Supplier = await supRepo.GetByIdAsync(entity.SupplierId.Value, cancellationToken);
        }

        // 3. Branch
        if (entity.BranchId.HasValue && entity.Branch == null)
        {
            var branchRepo = _repositoryFactory.CreateRepository<decorativeplant_be.Domain.Entities.Branch>();
            entity.Branch = await branchRepo.GetByIdAsync(entity.BranchId.Value, cancellationToken);
        }

        // 5. Batch Stocks (for inventory details)
        if (entity.BatchStocks == null || !entity.BatchStocks.Any())
        {
            var stockRepo = _repositoryFactory.CreateRepository<BatchStock>();
            entity.BatchStocks = await _context.BatchStocks
                .Include(bs => bs.Location)
                .Where(bs => bs.BatchId == entity.Id)
                .ToListAsync(cancellationToken);
        }

        return PlantBatchMapper.ToDto(entity);
    }
}
