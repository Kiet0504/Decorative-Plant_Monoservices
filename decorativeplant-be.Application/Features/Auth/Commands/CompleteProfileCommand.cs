using MediatR;

namespace decorativeplant_be.Application.Features.Auth.Commands;

public class CompleteProfileCommand : IRequest<bool>
{
    public Guid UserId { get; set; }

    public string? SunlightExposure { get; set; }
    public string? RoomTemperatureRange { get; set; }
    public string? HumidityLevel { get; set; }
    public string? WateringFrequency { get; set; }
    public string? ExperienceLevel { get; set; }
    public string? PlacementLocation { get; set; }
    public string? SpaceSize { get; set; }
    public bool? HasChildrenOrPets { get; set; }
    public List<string>? PlantGoals { get; set; }
    public string? PreferredStyle { get; set; }
    public string? BudgetRange { get; set; }
    public string? LocationCity { get; set; }
}
