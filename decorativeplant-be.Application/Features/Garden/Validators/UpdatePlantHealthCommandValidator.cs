using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Commands;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class UpdatePlantHealthCommandValidator : AbstractValidator<UpdatePlantHealthCommand>
{
    private static readonly string[] ValidHealth = ["healthy", "needs_attention", "struggling"];

    public UpdatePlantHealthCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Health)
            .NotEmpty().WithMessage("Health is required.")
            .Must(ValidHealth.Contains).WithMessage("Health must be one of: healthy, needs_attention, struggling.");
    }
}
