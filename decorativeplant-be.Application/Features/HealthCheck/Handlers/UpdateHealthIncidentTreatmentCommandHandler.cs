using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.HealthCheck.DTOs;
using decorativeplant_be.Application.Features.HealthCheck.Commands;
using decorativeplant_be.Domain.Entities;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.HealthCheck.Handlers;

public class UpdateHealthIncidentTreatmentCommandHandler : IRequestHandler<UpdateHealthIncidentTreatmentCommand, HealthIncidentDto>
{
    private readonly IRepositoryFactory _repositoryFactory;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateHealthIncidentTreatmentCommandHandler(IRepositoryFactory repositoryFactory, IUnitOfWork unitOfWork)
    {
        _repositoryFactory = repositoryFactory;
        _unitOfWork = unitOfWork;
    }

    public async Task<HealthIncidentDto> Handle(UpdateHealthIncidentTreatmentCommand request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<HealthIncident>();
        var entity = await repo.GetByIdAsync(request.Id, cancellationToken);
        if (entity == null)
        {
            throw new NotFoundException(nameof(HealthIncident), request.Id);
        }

        // Update TreatmentInfo (Task 9.4)
        var treatmentDetails = request.AdditionalTreatmentDetails ?? new Dictionary<string, object>();
        treatmentDetails["cost"] = request.TreatmentCost ?? 0;
        treatmentDetails["notes"] = request.TreatmentNotes ?? "";
        entity.TreatmentInfo = HealthIncidentMapper.BuildJson(treatmentDetails);

        // Update StatusInfo (Task 9.5)
        var currentStatus = new Dictionary<string, object>();
        if (entity.StatusInfo != null)
        {
            try { currentStatus = JsonSerializer.Deserialize<Dictionary<string, object>>(entity.StatusInfo.RootElement.GetRawText()) ?? new(); } catch {}
        }
        
        currentStatus["status"] = request.NewStatus;
        if (request.NewStatus == "Resolved")
        {
            currentStatus["resolved_at"] = DateTime.UtcNow;
            currentStatus["resolved_by"] = request.PerformedBy;
        }
        else
        {
            currentStatus["last_updated_at"] = DateTime.UtcNow;
            currentStatus["last_updated_by"] = request.PerformedBy;
        }
        
        entity.StatusInfo = HealthIncidentMapper.BuildJson(currentStatus);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Load relations for DTO
        if (entity.BatchId.HasValue)
        {
            var batchRepo = _repositoryFactory.CreateRepository<PlantBatch>();
            entity.Batch = await batchRepo.GetByIdAsync(entity.BatchId.Value, cancellationToken);
        }

        return HealthIncidentMapper.ToDto(entity);
    }
}
