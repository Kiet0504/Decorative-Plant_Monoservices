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
        // 1. Validate Parent Batch if provided
        if (request.ParentBatchId.HasValue)
        {
            var parentRepo = _repositoryFactory.CreateRepository<PlantBatch>();
            var parent = await parentRepo.GetByIdAsync(request.ParentBatchId.Value, cancellationToken);
            if (parent == null)
            {
                throw new NotFoundException(nameof(PlantBatch), request.ParentBatchId.Value);
            }
        }

        // 2. Generate Batch Code
        // BATCH-{yyyyMMdd}-{4_char_random}
        var datePart = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomPart = Guid.NewGuid().ToString().Substring(0, 4).ToUpper();
        var batchCode = $"BATCH-{datePart}-{randomPart}";

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
            CurrentTotalQuantity = request.InitialQuantity, // Assuming starting stock
            CreatedAt = DateTime.UtcNow
        };

        var repo = _repositoryFactory.CreateRepository<PlantBatch>();
        await repo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch relationships for full DTO
        if (entity.TaxonomyId.HasValue)
        {
            var taxRepo = _repositoryFactory.CreateRepository<PlantTaxonomy>();
            entity.Taxonomy = await taxRepo.GetByIdAsync(entity.TaxonomyId.Value, cancellationToken);
        }
        if (entity.SupplierId.HasValue)
        {
            var supRepo = _repositoryFactory.CreateRepository<Supplier>();
            entity.Supplier = await supRepo.GetByIdAsync(entity.SupplierId.Value, cancellationToken);
        }

        return PlantBatchMapper.ToDto(entity);
    }
}
