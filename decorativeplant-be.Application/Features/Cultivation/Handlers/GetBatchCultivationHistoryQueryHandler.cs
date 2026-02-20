using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Cultivation.DTOs;
using decorativeplant_be.Application.Features.Cultivation.Queries;
using decorativeplant_be.Domain.Entities;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Handlers;

public class GetBatchCultivationHistoryQueryHandler : IRequestHandler<GetBatchCultivationHistoryQuery, List<CultivationLogDto>>
{
    private readonly IRepositoryFactory _repositoryFactory;

    public GetBatchCultivationHistoryQueryHandler(IRepositoryFactory repositoryFactory)
    {
        _repositoryFactory = repositoryFactory;
    }

    public async Task<List<CultivationLogDto>> Handle(GetBatchCultivationHistoryQuery request, CancellationToken cancellationToken)
    {
        var repo = _repositoryFactory.CreateRepository<CultivationLog>();
        
        // Filter by BatchId
        var logs = await repo.FindAsync(c => c.BatchId == request.BatchId, cancellationToken);
        
        // Order by PerformedAt descending
        logs = logs.OrderByDescending(x => x.PerformedAt).ToList();

        // Map to DTO
        var dtos = new List<CultivationLogDto>();
        var userRepo = _repositoryFactory.CreateRepository<UserAccount>();
        var locRepo = _repositoryFactory.CreateRepository<InventoryLocation>();

        foreach (var log in logs)
        {
            if (log.PerformedBy.HasValue && log.PerformedByUser == null)
            {
                log.PerformedByUser = await userRepo.GetByIdAsync(log.PerformedBy.Value, cancellationToken);
            }
            if (log.LocationId.HasValue && log.Location == null)
            {
                log.Location = await locRepo.GetByIdAsync(log.LocationId.Value, cancellationToken);
            }

            dtos.Add(CultivationMapper.ToDto(log));
        }

        return dtos;
    }
}
