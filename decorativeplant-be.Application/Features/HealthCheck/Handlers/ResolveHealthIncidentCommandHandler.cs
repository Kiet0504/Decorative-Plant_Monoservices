using System.Text.Json;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Application.Features.HealthCheck.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.HealthCheck.Handlers;

public class ResolveHealthIncidentCommandHandler : IRequestHandler<ResolveHealthIncidentCommand, HealthIncidentDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public ResolveHealthIncidentCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<HealthIncidentDto> Handle(ResolveHealthIncidentCommand request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<HealthIncident>();
        var entity = await repo.GetByIdAsync(request.Id, cancellationToken);
        
        if (entity == null)
        {
            throw new NotFoundException(nameof(HealthIncident), request.Id);
        }

        // Update Description with Resolution Notes or append?
        // Let's append to Description or keep separate if entity had Resolution field. 
        // Entity doesn't have explicit Resolution field, so we might store it in Details or append to Description.
        // Let's append to description for now or assume TreatmentDetails holds it.
        // DTO has ResolutionNotes but entity doesn't. We'll put it in Details or TreatmentDetails.
        
        // Update Treatment Info
        if (request.TreatmentDetails != null)
        {
            entity.TreatmentInfo = HealthIncidentMapper.BuildJson(request.TreatmentDetails);
        }

        // Update Status Info
        // We need to preserve existing info (like reported_at) or merge.
        // Since we are using JsonDocument, we must deserialize, update, serialize.
        var statusDict = new Dictionary<string, object>();
        if (entity.StatusInfo != null)
        {
            try 
            {
                var existing = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.StatusInfo.RootElement.GetRawText());
                if (existing != null) statusDict = existing;
            } 
            catch { }
        }

        statusDict["status"] = "Resolved";
        statusDict["resolved_at"] = request.ResolvedAt ?? DateTime.UtcNow;
        if (request.ResolvedBy.HasValue)
        {
            statusDict["resolved_by"] = request.ResolvedBy.Value.ToString();
        }
        
        entity.StatusInfo = HealthIncidentMapper.BuildJson(statusDict);

        await repo.UpdateAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Fetch relations if needed
        if (entity.BatchId.HasValue && entity.Batch == null)
        {
            var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
            entity.Batch = await batchRepo.GetByIdAsync(entity.BatchId.Value, cancellationToken);
        }

        return HealthIncidentMapper.ToDto(entity);
    }
}
