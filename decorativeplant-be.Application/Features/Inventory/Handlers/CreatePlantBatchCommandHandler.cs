using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Inventory.Commands;
using decorativeplant_be.Application.Features.Inventory.DTOs;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Inventory.Handlers;

public class CreatePlantBatchCommandHandler : IRequestHandler<CreatePlantBatchCommand, PlantBatchDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePlantBatchCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<PlantBatchDto> Handle(CreatePlantBatchCommand request, CancellationToken cancellationToken)
    {
        var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
        var taxRepo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
        var branchRepo = _repositoryFactory.CreateRepository<Branch>();

        // 1. Validate Entities
        var taxonomy = await taxRepo.GetByIdAsync(request.TaxonomyId ?? Guid.Empty, cancellationToken);
        if (taxonomy == null) throw new NotFoundException(nameof(PlantTaxonomy), request.TaxonomyId ?? Guid.Empty);

        var branch = await branchRepo.GetByIdAsync(request.BranchId ?? Guid.Empty, cancellationToken);
        if (branch == null) throw new NotFoundException(nameof(Branch), request.BranchId ?? Guid.Empty);

        if (request.ParentBatchId.HasValue && !await batchRepo.ExistsAsync(x => x.Id == request.ParentBatchId.Value, cancellationToken))
        {
            throw new NotFoundException(nameof(PlantBatch), request.ParentBatchId.Value);
        }

        // 2. Generate Batch Code: {SPECIES}-{BRANCH}-{YEAR}-{SEQ}
        var speciesPart = (taxonomy.ScientificName.Length >= 4 ? taxonomy.ScientificName.Substring(0, 4) : taxonomy.ScientificName).ToUpper();
        var branchPart = branch.Code.ToUpper();
        var yearPart = DateTime.UtcNow.Year.ToString();
        
        // Calculate sequence
        var prefix = $"{speciesPart}-{branchPart}-{yearPart}-";
        var count = await batchRepo.CountAsync(x => x.BatchCode != null && x.BatchCode.StartsWith(prefix), cancellationToken);
        var seqPart = (count + 1).ToString("D4"); // 0001, 0002...

        var batchCode = $"{prefix}{seqPart}";

        var entity = new PlantBatch
        {
            Id = Guid.NewGuid(),
            BatchCode = batchCode,
            BranchId = request.BranchId,
            TaxonomyId = request.TaxonomyId,
            SupplierId = request.SupplierId,
            ParentBatchId = request.ParentBatchId,
            SourceInfo = PlantBatchMapper.BuildJson(request.SourceInfo),
            Specs = PlantBatchMapper.BuildJson(request.Specs),
            InitialQuantity = request.InitialQuantity,
            CurrentTotalQuantity = request.InitialQuantity,
            CreatedAt = DateTime.UtcNow
            // AiEmbedding remains null until AI process updates it
        };

        await batchRepo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch relationships for full DTO
        entity.Taxonomy = taxonomy;
        entity.Branch = branch;
        if (entity.SupplierId.HasValue)
        {
            var supRepo = _repositoryFactory.CreateRepository<Supplier>();
            entity.Supplier = await supRepo.GetByIdAsync(entity.SupplierId.Value, cancellationToken);
        }

        return PlantBatchMapper.ToDto(entity);
    }
}
