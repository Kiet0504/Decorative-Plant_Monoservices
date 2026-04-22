using FluentValidation;
using decorativeplant_be.Application.Features.Commerce.Orders.Commands;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Validators;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        
        RuleFor(x => x.Request.OrderType)
            .NotEmpty()
            .Must(t => t == "online" || t == "offline")
            .WithMessage("OrderType must be 'online' or 'offline'.");

        RuleFor(x => x.Request.FulfillmentMethod)
            .NotEmpty()
            .Must(m => m == "delivery" || m == "pickup")
            .WithMessage("FulfillmentMethod must be 'delivery' or 'pickup'.");

        RuleFor(x => x.Request.DeliveryAddress)
            .NotNull()
            .When(x => x.Request.FulfillmentMethod == "delivery")
            .WithMessage("DeliveryAddress is required for delivery orders.");

        RuleFor(x => x.Request.DeliveryAddress!.RecipientName).NotEmpty().When(x => x.Request.DeliveryAddress != null);
        RuleFor(x => x.Request.DeliveryAddress!.Phone).NotEmpty().When(x => x.Request.DeliveryAddress != null);
        RuleFor(x => x.Request.DeliveryAddress!.AddressLine1).NotEmpty().When(x => x.Request.DeliveryAddress != null);
        RuleFor(x => x.Request.DeliveryAddress!.Email)
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Request.DeliveryAddress?.Email))
            .WithMessage("Invalid recipient email.");

        RuleFor(x => x.Request.Items)
            .NotEmpty().WithMessage("Order must contain at least one item.");

        RuleForEach(x => x.Request.Items).SetValidator(new CreateOrderItemRequestValidator());
    }
}

public class CreateOrderItemRequestValidator : AbstractValidator<CreateOrderItemRequest>
{
    public CreateOrderItemRequestValidator()
    {
        RuleFor(i => i.ListingId).NotEmpty();
        RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Item quantity must be at least 1.");
    }
}

public class UpdateOrderStatusCommandValidator : AbstractValidator<UpdateOrderStatusCommand>
{
    public UpdateOrderStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Status).NotEmpty().WithMessage("Status is required.");
    }
}

public class ConfirmReceiptByTokenCommandValidator : AbstractValidator<ConfirmReceiptByTokenCommand>
{
    public ConfirmReceiptByTokenCommandValidator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
        RuleFor(x => x.Token).NotEmpty().MinimumLength(32).WithMessage("Token is required.");
    }
}

public class CreateOfflineBopisOrderValidator : AbstractValidator<CreateOfflineBopisOrderCommand>
{
    public CreateOfflineBopisOrderValidator()
    {
        RuleFor(x => x.BrandManagerId).NotEmpty();
        RuleFor(x => x.Request.PickupBranchId).NotEmpty();
        RuleFor(x => x.Request.CustomerName).NotEmpty();
        RuleFor(x => x.Request.CustomerPhone).NotEmpty();
        RuleFor(x => x.Request.DepositAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Request.Items).NotEmpty().WithMessage("Order must contain at least one item.");
        RuleForEach(x => x.Request.Items).SetValidator(new CreateOfflineBopisItemRequestValidator());
    }
}

public class CreateOfflineBopisItemRequestValidator : AbstractValidator<CreateOfflineBopisItemRequest>
{
    public CreateOfflineBopisItemRequestValidator()
    {
        RuleFor(i => i.ListingId).NotEmpty();
        RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("Item quantity must be at least 1.");
        RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0);
    }
}
