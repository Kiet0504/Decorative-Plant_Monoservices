using decorativeplant_be.Application.Common.DTOs.Garden;
using MediatR;

namespace decorativeplant_be.Application.Features.Garden.Commands;

/// <summary>
/// Command to add a care log (photo diary entry) to a garden plant.
/// </summary>
public class AddCareLogCommand : IRequest<CareLogDto>
{
    public Guid UserId { get; set; }

    public Guid PlantId { get; set; }

    public Guid? ScheduleId { get; set; }

    /// <summary>Preset slug (e.g. watered) or free-text label for what you did.</summary>
    public string ActionType { get; set; } = string.Empty;

    public string? Description { get; set; }

    public object? Products { get; set; }

    public string? Observations { get; set; }

    /// <summary>Optional: preset slug (thriving|okay|concerning) or free-text mood.</summary>
    public string? Mood { get; set; }

    public DateTime? PerformedAt { get; set; }

    public List<CareLogImageDto>? Images { get; set; }
}
