using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Commands;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class CreateGardenPlantCommandValidator : AbstractValidator<CreateGardenPlantCommand>
{
    private static readonly string[] ValidSources = ["purchased", "gift", "propagation", "manual_add"];
    private static readonly string[] ValidHealth = ["healthy", "needs_attention", "struggling"];
    private static readonly string[] ValidSizes = ["small", "medium", "large"];

    public CreateGardenPlantCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Nickname)
            .MaximumLength(100).WithMessage("Nickname must not exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.Nickname));

        RuleFor(x => x.Location)
            .MaximumLength(200).WithMessage("Location must not exceed 200 characters.")
            .When(x => !string.IsNullOrEmpty(x.Location));

        RuleFor(x => x.Source)
            .Must(s => string.IsNullOrEmpty(s) || ValidSources.Contains(s!))
            .WithMessage("Source must be one of: purchased, gift, propagation, manual_add.")
            .When(x => !string.IsNullOrEmpty(x.Source));

        RuleFor(x => x.Health)
            .Must(h => string.IsNullOrEmpty(h) || ValidHealth.Contains(h!))
            .WithMessage("Health must be one of: healthy, needs_attention, struggling.")
            .When(x => !string.IsNullOrEmpty(x.Health));

        RuleFor(x => x.Size)
            .Must(s => string.IsNullOrEmpty(s) || ValidSizes.Contains(s!))
            .WithMessage("Size must be one of: small, medium, large.")
            .When(x => !string.IsNullOrEmpty(x.Size));
    }
}
