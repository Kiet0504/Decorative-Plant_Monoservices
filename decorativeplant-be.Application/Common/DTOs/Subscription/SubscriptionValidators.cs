using FluentValidation;

namespace decorativeplant_be.Application.Common.DTOs.Subscription;

public class CreateSubscriptionRequestValidator : AbstractValidator<CreateSubscriptionRequest>
{
    public CreateSubscriptionRequestValidator()
    {
        RuleFor(x => x.PlanType)
            .NotEmpty().WithMessage("PlanType is required.")
            .MaximumLength(50).WithMessage("PlanType must not exceed 50 characters.");

        RuleFor(x => x.PaymentMethod)
            .MaximumLength(50).When(x => !string.IsNullOrEmpty(x.PaymentMethod))
            .WithMessage("PaymentMethod must not exceed 50 characters.");

        RuleFor(x => x.BillingCycle)
            .Must(bc => bc == null || bc == "Monthly" || bc == "Yearly")
            .WithMessage("BillingCycle must be either 'Monthly' or 'Yearly'.");

        // Premium subscriptions require payment method and billing cycle
        When(x => !string.IsNullOrEmpty(x.PlanType) && x.PlanType.Contains("Premium", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.PaymentMethod)
                .NotEmpty().WithMessage("PaymentMethod is required for Premium subscriptions.");

            RuleFor(x => x.BillingCycle)
                .NotEmpty().WithMessage("BillingCycle is required for Premium subscriptions.");
        });
    }
}

public class CancelSubscriptionRequestValidator : AbstractValidator<CancelSubscriptionRequest>
{
    public CancelSubscriptionRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Cancellation reason is required.")
            .MinimumLength(3).WithMessage("Cancellation reason must be at least 3 characters.")
            .MaximumLength(500).WithMessage("Cancellation reason must not exceed 500 characters.");
    }
}

public class UpgradeSubscriptionRequestValidator : AbstractValidator<UpgradeSubscriptionRequest>
{
    private static readonly string[] ValidPaymentMethods = { "VNPay", "MoMo", "PayOS", "ZaloPay", "Manual" };
    private static readonly string[] ValidBillingCycles = { "Monthly", "Yearly" };

    public UpgradeSubscriptionRequestValidator()
    {
        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("PaymentMethod is required.")
            .Must(pm => ValidPaymentMethods.Contains(pm, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"PaymentMethod must be one of: {string.Join(", ", ValidPaymentMethods)}");

        RuleFor(x => x.BillingCycle)
            .NotEmpty().WithMessage("BillingCycle is required.")
            .Must(bc => ValidBillingCycles.Contains(bc, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"BillingCycle must be one of: {string.Join(", ", ValidBillingCycles)}");

        RuleFor(x => x.AmountPaid)
            .NotEmpty().WithMessage("AmountPaid is required.");
    }
}
