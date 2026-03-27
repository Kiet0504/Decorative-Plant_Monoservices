using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

public class GetGrowthGalleryQuery : IRequest<GrowthTimelineDto>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public DateTime? Before { get; set; }

    public int Limit { get; set; } = 20;
}

