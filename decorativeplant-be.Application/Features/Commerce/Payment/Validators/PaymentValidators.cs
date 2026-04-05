using FluentValidation;
using decorativeplant_be.Application.Features.Commerce.Payment.Commands;

namespace decorativeplant_be.Application.Features.Commerce.Payment.Validators;

public class CreatePaymentCommandValidator : AbstractValidator<CreatePaymentCommand>
{
    public CreatePaymentCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Request.OrderIds).NotEmpty().WithMessage("At least one OrderId must be provided.");
        RuleFor(x => x.Request.ReturnUrl).NotEmpty();
        RuleFor(x => x.Request.CancelUrl).NotEmpty();
    }
}

public class HandlePayOSWebhookCommandValidator : AbstractValidator<HandlePayOSWebhookCommand>
{
    public HandlePayOSWebhookCommandValidator()
    {
        RuleFor(x => x.Webhook).NotNull();
    }
}
