using FluentValidation;
using decorativeplant_be.Application.Features.Commerce.Shipping.Commands;

namespace decorativeplant_be.Application.Features.Commerce.Shipping.Validators;

public class CreateShippingCommandValidator : AbstractValidator<CreateShippingCommand>
{
    public CreateShippingCommandValidator()
    {
        RuleFor(x => x.Request.OrderId).NotEmpty();
        RuleFor(x => x.Request.Carrier).NotEmpty();
        RuleFor(x => x.Request.Method).NotEmpty();
    }
}

public class UpdateShippingStatusCommandValidator : AbstractValidator<UpdateShippingStatusCommand>
{
    public UpdateShippingStatusCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Status).NotEmpty();
    }
}

public class CreateShippingZoneCommandValidator : AbstractValidator<CreateShippingZoneCommand>
{
    public CreateShippingZoneCommandValidator()
    {
        RuleFor(x => x.Request.BranchId).NotEmpty();
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(100);
    }
}

public class UpdateShippingZoneCommandValidator : AbstractValidator<UpdateShippingZoneCommand>
{
    public UpdateShippingZoneCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Request.Name).MaximumLength(100).When(x => x.Request.Name != null);
    }
}
