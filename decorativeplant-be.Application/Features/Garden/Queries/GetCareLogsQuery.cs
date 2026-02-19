using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

/// <summary>
/// Query to list care logs for a garden plant.
/// </summary>
public class GetCareLogsQuery : IRequest<List<CareLogDto>>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }
}
