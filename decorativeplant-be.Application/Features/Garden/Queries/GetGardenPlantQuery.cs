using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Queries;

/// <summary>
/// Query to get a single garden plant by ID.
/// </summary>
public class GetGardenPlantQuery : IRequest<GardenPlantDto>
{
    public Guid UserId { get; set; }

    public Guid Id { get; set; }
}
