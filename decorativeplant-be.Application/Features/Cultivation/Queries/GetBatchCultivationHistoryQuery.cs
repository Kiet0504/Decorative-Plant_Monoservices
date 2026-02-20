using decorativeplant_be.Application.Features.Cultivation.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Queries;

public class GetBatchCultivationHistoryQuery : IRequest<List<CultivationLogDto>>
{
    public Guid BatchId { get; set; }
}
