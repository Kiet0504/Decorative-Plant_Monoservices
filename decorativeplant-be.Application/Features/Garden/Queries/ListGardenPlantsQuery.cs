using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

/// <summary>
/// Query to list garden plants for a user with pagination and filters.
/// </summary>
public class ListGardenPlantsQuery : IRequest<PagedResultDto<GardenPlantDto>>
{
    public Guid UserId { get; set; }

    public bool IncludeArchived { get; set; }

    public string? HealthFilter { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}
