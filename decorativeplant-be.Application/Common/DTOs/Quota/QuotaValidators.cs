using FluentValidation;

namespace decorativeplant_be.Application.Common.DTOs.Quota;

public class ConsumeQuotaRequestValidator : AbstractValidator<ConsumeQuotaRequest>
{
    public ConsumeQuotaRequestValidator()
    {
        RuleFor(x => x.FeatureKey)
            .NotEmpty().WithMessage("FeatureKey is required.")
            .MaximumLength(100).WithMessage("FeatureKey must not exceed 100 characters.");
    }
}

public class CheckQuotaRequestValidator : AbstractValidator<CheckQuotaRequest>
{
    public CheckQuotaRequestValidator()
    {
        RuleFor(x => x.FeatureKey)
            .NotEmpty().WithMessage("FeatureKey is required.")
            .MaximumLength(100).WithMessage("FeatureKey must not exceed 100 characters.");
    }
}
