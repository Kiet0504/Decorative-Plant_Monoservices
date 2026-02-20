using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Application.Features.Inventory.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class GetLowStockQueryHandler : IRequestHandler<GetLowStockQuery, List<LowStockItemDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public GetLowStockQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<List<LowStockItemDto>> Handle(GetLowStockQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<PlantBatch>();
        
        // Filter: Quantity < Threshold AND (Branch matches OR Branch is null/global search)
        // Also ensure we only check active batches (Active status logic might be in JSON or implied by quantity > 0, here we just check raw quantity)
        var batches = await repo.FindAsync(b => 
            (b.CurrentTotalQuantity ?? 0) < request.Threshold &&
            (!request.BranchId.HasValue || b.BranchId == request.BranchId), 
            cancellationToken);

        // Map to DTO
        var dtos = new List<LowStockItemDto>();
        var taxRepo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
        var branchRepo = _repositoryFactory.CreateRepository<Branch>();

        foreach (var batch in batches)
        {
            // Load relations if missing
            string speciesName = "Unknown";
            if (batch.TaxonomyId.HasValue)
            {
                if (batch.Taxonomy == null) 
                    batch.Taxonomy = await taxRepo.GetByIdAsync(batch.TaxonomyId.Value, cancellationToken);
                speciesName = batch.Taxonomy?.ScientificName ?? "Unknown";
            }

            string branchName = "Unknown";
            if (batch.BranchId.HasValue)
            {
                if (batch.Branch == null)
                    batch.Branch = await branchRepo.GetByIdAsync(batch.BranchId.Value, cancellationToken);
                branchName = batch.Branch?.Name ?? "Unknown";
            }

            dtos.Add(new LowStockItemDto
            {
                BatchId = batch.Id,
                BatchCode = batch.BatchCode ?? "N/A",
                SpeciesName = speciesName,
                BranchId = batch.BranchId,
                BranchName = branchName,
                CurrentQuantity = batch.CurrentTotalQuantity ?? 0
            });
        }

        return dtos;
    }
}
