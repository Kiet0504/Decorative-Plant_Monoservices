using FluentValidation;
using decorativeplant_be.Application.Features.Commerce.Promotions.Commands;

namespace decorativeplant_be.Application.Features.Commerce.Promotions.Validators;

public class CreatePromotionCommandValidator : AbstractValidator<CreatePromotionCommand>
{
    public CreatePromotionCommandValidator()
    {
        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Promotion name is required.")
            .MaximumLength(150).WithMessage("Promotion name cannot exceed 150 characters.");

        RuleFor(x => x.Request.DiscountType)
            .NotEmpty()
            .Must(t => t == "percentage" || t == "fixed_amount")
            .WithMessage("Invalid discount type.");

        RuleFor(x => x.Request.Value)
            .NotEmpty().WithMessage("Value is required.")
            .Must(v => decimal.TryParse(v, out var val) && val >= 0)
            .WithMessage("Value must be a valid non-negative number.");
            
        RuleFor(x => x.Request.MinOrder)
            .Must(v => v == null || (decimal.TryParse(v, out var val) && val >= 0))
            .WithMessage("MinOrder must be a valid non-negative number.")
            .When(x => x.Request.MinOrder != null);
    }
}

public class UpdatePromotionCommandValidator : AbstractValidator<UpdatePromotionCommand>
{
    public UpdatePromotionCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Request.Name)
            .MaximumLength(150).WithMessage("Promotion name cannot exceed 150 characters.")
            .When(x => x.Request.Name != null);

        RuleFor(x => x.Request.Value)
            .Must(v => decimal.TryParse(v, out var val) && val >= 0)
            .WithMessage("Value must be a valid non-negative number.")
            .When(x => x.Request.Value != null);
    }
}
