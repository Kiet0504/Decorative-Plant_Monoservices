using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Commands;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class AddGrowthMilestoneCommandValidator : AbstractValidator<AddGrowthMilestoneCommand>
{
    private static readonly string[] ValidTypes = ["first_leaf", "new_growth", "flowering", "repotted", "other"];

    public AddGrowthMilestoneCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlantId).NotEmpty();
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Type is required.")
            .Must(ValidTypes.Contains).WithMessage("Type must be one of: first_leaf, new_growth, flowering, repotted, other.");
        RuleFor(x => x.OccurredAt).NotEmpty().WithMessage("OccurredAt is required.");
    }
}
