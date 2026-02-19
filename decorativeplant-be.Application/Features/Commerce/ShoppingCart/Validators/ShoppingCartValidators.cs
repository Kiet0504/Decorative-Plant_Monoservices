using FluentValidation;
using decorativeplant_be.Application.Features.Commerce.ShoppingCart.Commands;

namespace decorativeplant_be.Application.Features.Commerce.ShoppingCart.Validators;

public class AddToCartCommandValidator : AbstractValidator<AddToCartCommand>
{
    public AddToCartCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request.ListingId).NotEmpty();
        RuleFor(x => x.Request.Quantity).GreaterThan(0).WithMessage("Quantity must be at least 1.");
    }
}

public class UpdateCartItemCommandValidator : AbstractValidator<UpdateCartItemCommand>
{
    public UpdateCartItemCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ListingId).NotEmpty();
        RuleFor(x => x.Request.Quantity).GreaterThanOrEqualTo(0).WithMessage("Quantity cannot be negative.");
    }
}
