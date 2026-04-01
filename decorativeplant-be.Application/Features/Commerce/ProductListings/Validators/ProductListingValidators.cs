using FluentValidation;
using decorativeplant_be.Application.Features.Commerce.ProductListings.Commands;

namespace decorativeplant_be.Application.Features.Commerce.ProductListings.Validators;

public class CreateProductListingCommandValidator : AbstractValidator<CreateProductListingCommand>
{
    public CreateProductListingCommandValidator()
    {
        RuleFor(x => x.Request.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.");

        RuleFor(x => x.Request.Price)
            .NotEmpty().WithMessage("Price is required.")
            .Must(p => decimal.TryParse(p, out var d) && d >= 0)
            .WithMessage("Price must be a valid non-negative number.");

        RuleFor(x => x.Request.MinOrder)
            .GreaterThan(0).WithMessage("Minimum order must be at least 1.");

        RuleFor(x => x.Request.MaxOrder)
            .GreaterThanOrEqualTo(x => x.Request.MinOrder)
            .WithMessage("Maximum order must be greater than or equal to minimum order.");

        RuleFor(x => x.Request.Status)
            .NotEmpty().WithMessage("Status is required.");
    }
}

public class UpdateProductListingCommandValidator : AbstractValidator<UpdateProductListingCommand>
{
    public UpdateProductListingCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.Request.Title)
            .MaximumLength(200).WithMessage("Title cannot exceed 200 characters.")
            .When(x => x.Request.Title != null);

        RuleFor(x => x.Request.Price)
            .Must(p => p == null || (decimal.TryParse(p, out var d) && d >= 0))
            .WithMessage("Price must be a valid non-negative number.")
            .When(x => x.Request.Price != null);

        RuleFor(x => x.Request.MinOrder)
            .GreaterThan(0).WithMessage("Minimum order must be at least 1.")
            .When(x => x.Request.MinOrder != null);

        RuleFor(x => x.Request.MaxOrder)
            .GreaterThanOrEqualTo(x => x.Request.MinOrder ?? 0)
            .WithMessage("Maximum order must be greater than or equal to minimum order.")
            .When(x => x.Request.MaxOrder != null && x.Request.MinOrder != null);
    }
}
