using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Cultivation.DTOs;
using decorativeplant_be.Application.Features.Cultivation.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using decorativeplant_be.Application.Common.Exceptions;

namespace decorativeplant_be.Application.Features.Cultivation.Handlers;

public class LogCultivationActivityCommandHandler : IRequestHandler<LogCultivationActivityCommand, CultivationLogDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public LogCultivationActivityCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<CultivationLogDto> Handle(LogCultivationActivityCommand request, CancellationToken cancellationToken)
    {
        // Validation: Batch OR Location must be provided (or both)
        if (!request.BatchId.HasValue && !request.LocationId.HasValue)
        {
            throw new ValidationException("Either BatchId or LocationId must be provided for a cultivation log.");
        }

        if (request.BatchId.HasValue)
        {
            var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
            if (!await batchRepo.ExistsAsync(x => x.Id == request.BatchId.Value, cancellationToken))
                throw new NotFoundException(nameof(PlantBatch), request.BatchId.Value);
        }

        if (request.LocationId.HasValue)
        {
            var locRepo = _repositoryFactory.CreateRepository<InventoryLocation>();
            if (!await locRepo.ExistsAsync(x => x.Id == request.LocationId.Value, cancellationToken))
                throw new NotFoundException(nameof(InventoryLocation), request.LocationId.Value);
        }

        var entity = new CultivationLog
        {
            Id = Guid.NewGuid(),
            BatchId = request.BatchId,
            LocationId = request.LocationId,
            ActivityType = request.ActivityType,
            Description = request.Description,
            Details = CultivationMapper.BuildJson(request.Details),
            PerformedBy = request.PerformedBy,
            PerformedAt = request.PerformedAt ?? DateTime.UtcNow
        };

        var repo = _repositoryFactory.CreateRepository<CultivationLog>();
        await repo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch relations for DTO
        if (entity.BatchId.HasValue)
        {
            var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
            entity.Batch = await batchRepo.GetByIdAsync(entity.BatchId.Value, cancellationToken);
        }
        
        if (entity.LocationId.HasValue)
        {
            var locRepo = _repositoryFactory.CreateRepository<InventoryLocation>();
            entity.Location = await locRepo.GetByIdAsync(entity.LocationId.Value, cancellationToken);
        }
        
        if (entity.PerformedBy.HasValue)
        {
            var userRepo = _repositoryFactory.CreateRepository<UserAccount>();
            entity.PerformedByUser = await userRepo.GetByIdAsync(entity.PerformedBy.Value, cancellationToken);
        }

        return CultivationMapper.ToDto(entity);
    }
}
