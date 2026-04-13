using FluentValidation;
using decorativeplant_be.Application.Features.Auth.Commands;

namespace decorativeplant_be.Application.Features.Auth.Validators;

public class CompleteProfileCommandValidator : AbstractValidator<CompleteProfileCommand>
{
    private static readonly string[] ValidSunlightExposure = { "low_light", "indirect", "filtered", "morning_3h", "full_sun_6h" };
    private static readonly string[] ValidRoomTemperatureRange = { "cool", "moderate", "warm" };
    private static readonly string[] ValidHumidityLevel = { "dry", "moderate", "humid" };
    private static readonly string[] ValidWateringFrequency = { "daily", "every_2_3_days", "weekly", "rarely" };
    private static readonly string[] ValidPlacementLocation = { "desk", "living_room", "bedroom", "hallway", "office", "balcony" };
    private static readonly string[] ValidSpaceSize = { "small", "medium", "large" };
    private static readonly string[] ValidPreferredStyle = { "tropical", "minimalist", "classic" };
    private static readonly string[] ValidBudgetRange = { "low", "medium", "unlimited" };
    private static readonly string[] ValidExperienceLevel = { "beginner", "intermediate", "expert" };
    private static readonly string[] ValidPlantGoals = { "decoration", "air_purification", "easy_care", "flowering" };

    public CompleteProfileCommandValidator()
    {
        // Relaxed validation to support both onboarding AND partial updates
        RuleFor(x => x.ExperienceLevel)
            .Must(value => string.IsNullOrEmpty(value) || ValidExperienceLevel.Contains(value))
            .WithMessage($"ExperienceLevel must be one of: {string.Join(", ", ValidExperienceLevel)}");

        RuleFor(x => x.SunlightExposure)
            .Must(value => string.IsNullOrEmpty(value) || ValidSunlightExposure.Contains(value))
            .WithMessage($"SunlightExposure must be one of: {string.Join(", ", ValidSunlightExposure)}");

        RuleFor(x => x.RoomTemperatureRange)
            .Must(value => string.IsNullOrEmpty(value) || ValidRoomTemperatureRange.Contains(value))
            .WithMessage($"RoomTemperatureRange must be one of: {string.Join(", ", ValidRoomTemperatureRange)}");

        RuleFor(x => x.HumidityLevel)
            .Must(value => string.IsNullOrEmpty(value) || ValidHumidityLevel.Contains(value))
            .WithMessage($"HumidityLevel must be one of: {string.Join(", ", ValidHumidityLevel)}");

        RuleFor(x => x.WateringFrequency)
            .Must(value => string.IsNullOrEmpty(value) || ValidWateringFrequency.Contains(value))
            .WithMessage($"WateringFrequency must be one of: {string.Join(", ", ValidWateringFrequency)}");

        RuleFor(x => x.PlacementLocation)
            .Must(locations => locations == null || locations.Count == 0 || locations.All(loc => ValidPlacementLocation.Contains(loc)))
            .WithMessage($"PlacementLocation must only contain values from: {string.Join(", ", ValidPlacementLocation)}");

        RuleFor(x => x.SpaceSize)
            .Must(sizes => sizes == null || sizes.Count == 0 || sizes.All(size => ValidSpaceSize.Contains(size)))
            .WithMessage($"SpaceSize must only contain values from: {string.Join(", ", ValidSpaceSize)}");

        RuleFor(x => x.PreferredStyle)
            .Must(styles => styles == null || styles.Count == 0 || styles.All(style => ValidPreferredStyle.Contains(style)))
            .WithMessage($"PreferredStyle must only contain values from: {string.Join(", ", ValidPreferredStyle)}");

        RuleFor(x => x.BudgetRange)
            .Must(value => string.IsNullOrEmpty(value) || ValidBudgetRange.Contains(value))
            .WithMessage($"BudgetRange must be one of: {string.Join(", ", ValidBudgetRange)}");

        RuleFor(x => x.PlantGoals)
            .Must(goals => goals == null || goals.Count == 0 || goals.All(g => ValidPlantGoals.Contains(g)))
            .WithMessage($"PlantGoals must only contain values from: {string.Join(", ", ValidPlantGoals)}");

        RuleFor(x => x.LocationCity)
            .MaximumLength(100)
            .WithMessage("LocationCity must not exceed 100 characters.");

        RuleFor(x => x.Bio)
            .MaximumLength(2000)
            .When(x => !string.IsNullOrEmpty(x.Bio))
            .WithMessage("Bio must not exceed 2000 characters.");

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(2048)
            .When(x => !string.IsNullOrEmpty(x.AvatarUrl))
            .WithMessage("AvatarUrl must not exceed 2048 characters.");

        RuleFor(x => x.HardinessZone)
            .MaximumLength(32)
            .When(x => !string.IsNullOrEmpty(x.HardinessZone))
            .WithMessage("HardinessZone must not exceed 32 characters.");
    }
}
