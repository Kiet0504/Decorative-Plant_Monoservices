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

    // Changed to List to support multiple selections - will be concatenated as comma-separated string in handler
    public List<string>? PlacementLocation { get; set; }
    public List<string>? SpaceSize { get; set; }

    public bool? HasChildrenOrPets { get; set; }
    public List<string>? PlantGoals { get; set; }

    // Changed to List to support multiple selections - will be concatenated as comma-separated string in handler
    public List<string>? PreferredStyle { get; set; }

    public string? BudgetRange { get; set; }
    public string? LocationCity { get; set; }

    public string? FullName { get; set; }
    public string? Phone { get; set; }
}
