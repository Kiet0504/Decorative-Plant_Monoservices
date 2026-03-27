using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Commands;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class AddGrowthPhotoCommandValidator : AbstractValidator<AddGrowthPhotoCommand>
{
    public AddGrowthPhotoCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.PlantId).NotEmpty();
        RuleFor(x => x.ImageUrl).NotEmpty().MaximumLength(2048);
        RuleFor(x => x.Caption).MaximumLength(500);
    }
}

