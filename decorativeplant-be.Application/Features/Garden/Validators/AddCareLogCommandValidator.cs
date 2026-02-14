using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Commands;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class AddCareLogCommandValidator : AbstractValidator<AddCareLogCommand>
{
    private static readonly string[] ValidActionTypes = ["watered", "fertilized", "pruned", "repotted", "inspected"];
    private static readonly string[] ValidMoods = ["thriving", "okay", "concerning"];

    public AddCareLogCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlantId).NotEmpty();
        RuleFor(x => x.ActionType)
            .NotEmpty().WithMessage("Action type is required.")
            .Must(ValidActionTypes.Contains).WithMessage("Action type must be one of: watered, fertilized, pruned, repotted, inspected.");
        RuleFor(x => x.Mood)
            .Must(m => string.IsNullOrEmpty(m) || ValidMoods.Contains(m!))
            .When(x => !string.IsNullOrEmpty(x.Mood));
    }
}
