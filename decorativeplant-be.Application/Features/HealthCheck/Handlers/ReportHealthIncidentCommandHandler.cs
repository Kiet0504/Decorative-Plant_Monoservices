using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Application.Features.HealthCheck.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using decorativeplant_be.Application.Common.Exceptions;

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
        if (!await batchRepo.ExistsAsync(x => x.Id == request.BatchId, cancellationToken))
            throw new NotFoundException(nameof(PlantBatch), request.BatchId);

        // Construct StatusInfo JSON
        var statusInfo = new Dictionary<string, object>
        {
            { "status", "Reported" },
            { "reported_at", request.ReportedAt ?? DateTime.UtcNow },
            { "reported_by", request.ReportedBy?.ToString() ?? "" }
        };

        // Construct Images JSON
        Dictionary<string, object>? images = null;
        if (request.ImageUrls != null && request.ImageUrls.Any())
        {
            images = new Dictionary<string, object> { { "urls", request.ImageUrls } };
        }

        var entity = new HealthIncident
        {
            Id = Guid.NewGuid(),
            BatchId = request.BatchId,
            IncidentType = request.IncidentType,
            Severity = request.Severity,
            Description = request.Description,
            StatusInfo = HealthIncidentMapper.BuildJson(statusInfo),
            Images = HealthIncidentMapper.BuildJson(images)
        };

        var repo = _repositoryFactory.CreateRepository<HealthIncident>();
        await repo.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch Batch for DTO
        entity.Batch = await batchRepo.GetByIdAsync(request.BatchId, cancellationToken);

        return HealthIncidentMapper.ToDto(entity);
    }
}
