using FluentValidation;
using decorativeplant_be.Application.Features.Diagnosis.Commands;

namespace decorativeplant_be.Application.Features.Diagnosis.Validators;

public class SubmitFeedbackCommandValidator : AbstractValidator<SubmitFeedbackCommand>
{
    private static readonly string[] ValidFeedback = ["helpful", "not_helpful", "wrong"];

    public SubmitFeedbackCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.DiagnosisId).NotEmpty();
        RuleFor(x => x.UserFeedback)
            .NotEmpty().WithMessage("User feedback is required.")
            .Must(ValidFeedback.Contains).WithMessage("User feedback must be one of: helpful, not_helpful, wrong.");
    }
}
