using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

/// <summary>
/// Query to get a single care log by ID.
/// </summary>
public class GetCareLogQuery : IRequest<CareLogDto>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public Guid LogId { get; set; }
}
