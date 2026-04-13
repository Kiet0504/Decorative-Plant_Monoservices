using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Commands;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class AddCareLogCommandValidator : AbstractValidator<AddCareLogCommand>
{
    public AddCareLogCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlantId).NotEmpty();
        RuleFor(x => x.ActionType)
            .NotEmpty().WithMessage("Action type is required.")
            .MaximumLength(200)
            .Must(s => !string.IsNullOrWhiteSpace(s))
            .WithMessage("Action type cannot be only whitespace.");
        RuleFor(x => x.Mood)
            .MaximumLength(200)
            .When(x => !string.IsNullOrEmpty(x.Mood));
    }
}
