using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Commands;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class DeleteGardenPlantCommandValidator : AbstractValidator<DeleteGardenPlantCommand>
{
    public DeleteGardenPlantCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Id).NotEmpty();
    }
}
