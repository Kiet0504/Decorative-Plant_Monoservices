using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

/// <summary>
/// Command to create a new garden plant.
/// </summary>
public class CreateGardenPlantCommand : IRequest<GardenPlantDto>
{
    /// <summary>Set by controller from JWT. Required.</summary>
    public Guid UserId { get; set; }

    public Guid? TaxonomyId { get; set; }

    public string? Nickname { get; set; }

    public string? Location { get; set; }

    /// <summary>purchased|gift|propagation|manual_add</summary>
    public string? Source { get; set; }

    /// <summary>ISO date string</summary>
    public string? AdoptedDate { get; set; }

    /// <summary>healthy|needs_attention|struggling</summary>
    public string? Health { get; set; }

    /// <summary>small|medium|large</summary>
    public string? Size { get; set; }

    public string? ImageUrl { get; set; }
}
