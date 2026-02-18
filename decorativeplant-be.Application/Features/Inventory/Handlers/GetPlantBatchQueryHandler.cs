using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class GetPlantBatchQueryHandler : IRequestHandler<GetPlantBatchQuery, PlantBatchDto>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public GetPlantBatchQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
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

        // 3. Parent Batch (for traceability)
        if (entity.ParentBatchId.HasValue && entity.ParentBatch == null)
        {
            // Avoid deep recursion, just get immediate parent for now.
            var parentRepo = _repositoryFactory.CreateRepository<PlantBatch>();
            entity.ParentBatch = await parentRepo.GetByIdAsync(entity.ParentBatchId.Value, cancellationToken);
        }

        return PlantBatchMapper.ToDto(entity);
    }
}
