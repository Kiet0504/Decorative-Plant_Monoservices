using FluentValidation;
using decorativeplant_be.Application.Features.Garden.Commands;

namespace decorativeplant_be.Application.Features.Garden.Validators;

public class ImportGardenPlantsFromPurchaseCommandValidator : AbstractValidator<ImportGardenPlantsFromPurchaseCommand>
{
    public ImportGardenPlantsFromPurchaseCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.OrderItemIds)
            .NotNull()
            .Must(ids => ids != null && ids.Count > 0)
            .WithMessage("orderItemIds is required.");

        RuleForEach(x => x.OrderItemIds).NotEmpty();

        RuleFor(x => x.OrderItemIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("orderItemIds must not contain duplicates.");

        RuleFor(x => x.OrderItemIds.Count)
            .LessThanOrEqualTo(50)
            .WithMessage("Too many orderItemIds. Maximum is 50 per request.");

        RuleFor(x => x.Nickname).MaximumLength(100);
        RuleFor(x => x.Location).MaximumLength(100);
        RuleFor(x => x.Health).MaximumLength(50);
        RuleFor(x => x.Size).MaximumLength(50);
        RuleFor(x => x.ImageUrl).MaximumLength(2048);
    }
}

