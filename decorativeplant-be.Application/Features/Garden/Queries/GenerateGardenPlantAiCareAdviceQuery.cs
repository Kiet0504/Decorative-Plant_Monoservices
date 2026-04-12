using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

public sealed class GenerateGardenPlantAiCareAdviceQuery : IRequest<AiCareAdviceDto>
{
    public Guid UserId { get; set; }
    public Guid PlantId { get; set; }
    public bool Force { get; set; } = false;
}

