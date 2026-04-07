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
        // Onboarding completion requires all fields (aligned with React zod onboardingSchema)
        RuleFor(x => x.ExperienceLevel)
            .NotEmpty()
            .WithMessage("ExperienceLevel is required.")
            .Must(value => ValidExperienceLevel.Contains(value))
            .WithMessage($"ExperienceLevel must be one of: {string.Join(", ", ValidExperienceLevel)}");

        RuleFor(x => x.SunlightExposure)
            .NotEmpty()
            .WithMessage("SunlightExposure is required.")
            .Must(value => ValidSunlightExposure.Contains(value))
            .WithMessage($"SunlightExposure must be one of: {string.Join(", ", ValidSunlightExposure)}");

        RuleFor(x => x.RoomTemperatureRange)
            .NotEmpty()
            .WithMessage("RoomTemperatureRange is required.")
            .Must(value => ValidRoomTemperatureRange.Contains(value))
            .WithMessage($"RoomTemperatureRange must be one of: {string.Join(", ", ValidRoomTemperatureRange)}");

        RuleFor(x => x.HumidityLevel)
            .NotEmpty()
            .WithMessage("HumidityLevel is required.")
            .Must(value => ValidHumidityLevel.Contains(value))
            .WithMessage($"HumidityLevel must be one of: {string.Join(", ", ValidHumidityLevel)}");

        RuleFor(x => x.WateringFrequency)
            .NotEmpty()
            .WithMessage("WateringFrequency is required.")
            .Must(value => ValidWateringFrequency.Contains(value))
            .WithMessage($"WateringFrequency must be one of: {string.Join(", ", ValidWateringFrequency)}");

        RuleFor(x => x.PlacementLocation)
            .NotNull()
            .WithMessage("PlacementLocation is required.")
            .Must(locations => locations!.Count > 0)
            .WithMessage("PlacementLocation must contain at least one value.")
            .Must(locations => locations!.All(loc => ValidPlacementLocation.Contains(loc)))
            .WithMessage($"PlacementLocation must only contain values from: {string.Join(", ", ValidPlacementLocation)}");

        RuleFor(x => x.SpaceSize)
            .NotNull()
            .WithMessage("SpaceSize is required.")
            .Must(sizes => sizes!.Count > 0)
            .WithMessage("SpaceSize must contain at least one value.")
            .Must(sizes => sizes!.All(size => ValidSpaceSize.Contains(size)))
            .WithMessage($"SpaceSize must only contain values from: {string.Join(", ", ValidSpaceSize)}");

        RuleFor(x => x.PreferredStyle)
            .NotNull()
            .WithMessage("PreferredStyle is required.")
            .Must(styles => styles!.Count > 0)
            .WithMessage("PreferredStyle must contain at least one value.")
            .Must(styles => styles!.All(style => ValidPreferredStyle.Contains(style)))
            .WithMessage($"PreferredStyle must only contain values from: {string.Join(", ", ValidPreferredStyle)}");

        RuleFor(x => x.BudgetRange)
            .NotEmpty()
            .WithMessage("BudgetRange is required.")
            .Must(value => ValidBudgetRange.Contains(value))
            .WithMessage($"BudgetRange must be one of: {string.Join(", ", ValidBudgetRange)}");

        RuleFor(x => x.PlantGoals)
            .NotNull()
            .WithMessage("PlantGoals is required.")
            .Must(goals => goals!.Count > 0)
            .WithMessage("PlantGoals must contain at least one value.")
            .Must(goals => goals!.All(g => ValidPlantGoals.Contains(g)))
            .WithMessage($"PlantGoals must only contain values from: {string.Join(", ", ValidPlantGoals)}");

        RuleFor(x => x.LocationCity)
            .NotEmpty()
            .WithMessage("LocationCity is required.")
            .MaximumLength(100)
            .WithMessage("LocationCity must not exceed 100 characters.");

        RuleFor(x => x.HasChildrenOrPets)
            .NotNull()
            .WithMessage("HasChildrenOrPets is required.");
    }
}
