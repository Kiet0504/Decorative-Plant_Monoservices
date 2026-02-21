using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Application.Features.HealthCheck.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Handlers;

public class ReportHealthIncidentCommandHandler : IRequestHandler<ReportHealthIncidentCommand, HealthIncidentDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public ReportHealthIncidentCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<HealthIncidentDto> Handle(ReportHealthIncidentCommand request, CancellationToken cancellationToken)
    {
        var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
        var batch = await batchRepo.GetByIdAsync(request.BatchId, cancellationToken);
        if (batch == null)
        {
            throw new NotFoundException(nameof(PlantBatch), request.BatchId);
        }

        var statusInfo = new
        {
            status = "Reported",
            reported_at = request.ReportedAt ?? DateTime.UtcNow,
            reported_by = request.PerformedBy
        };

        var entity = new HealthIncident
        {
            Id = Guid.NewGuid(),
            BatchId = request.BatchId,
            IncidentType = request.IncidentType,
            Severity = request.Severity,
            Description = request.Description,
            Details = HealthIncidentMapper.BuildJson(new { ai_embedding = request.AiEmbedding }),
            Images = HealthIncidentMapper.BuildJson(request.ImageUrls),
            StatusInfo = HealthIncidentMapper.BuildJson(statusInfo)
        };

        var repo = _repositoryFactory.CreateRepository<HealthIncident>();
        await repo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch relations for DTO
        entity.Batch = batch;
        if (request.PerformedBy.HasValue)
        {
            var userRepo = _repositoryFactory.CreateRepository<UserAccount>();
            var user = await userRepo.FirstOrDefaultAsync(u => u.Id == request.PerformedBy.Value, cancellationToken);
            // In a real app, Mapper or Handler would use the joined entity.
            // For simplicity in this generic pattern, we'll rely on the mapper's JSON extraction for names if needed,
            // or fetch the user here.
            // Let's just pass the entity to mapper.
        }

        return HealthIncidentMapper.ToDto(entity);
    }
}
