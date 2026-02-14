using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

/// <summary>
/// Command to update an existing garden plant.
/// </summary>
public class UpdateGardenPlantCommand : IRequest<GardenPlantDto>
{
    public Guid UserId { get; set; }

    public Guid Id { get; set; }

    public Guid? TaxonomyId { get; set; }

    public string? Nickname { get; set; }

    public string? Location { get; set; }

    public string? Source { get; set; }

    public string? AdoptedDate { get; set; }

    public string? Health { get; set; }

    public string? Size { get; set; }

    public string? ImageUrl { get; set; }
}
