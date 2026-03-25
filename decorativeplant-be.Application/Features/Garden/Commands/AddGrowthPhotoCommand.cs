using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

public class AddGrowthPhotoCommand : IRequest<GrowthPhotoEntryDto>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public string ImageUrl { get; set; } = string.Empty;

    public string? Caption { get; set; }

    public bool SetAsAvatar { get; set; } = false;

    public DateTime? PerformedAt { get; set; }
}

