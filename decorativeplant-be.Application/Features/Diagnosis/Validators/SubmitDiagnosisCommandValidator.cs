using FluentValidation;
using decorativeplant_be.Application.Features.Diagnosis.Commands;

namespace decorativeplant_be.Application.Features.Diagnosis.Validators;

public class SubmitDiagnosisCommandValidator : AbstractValidator<SubmitDiagnosisCommand>
{
    public SubmitDiagnosisCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.ImageUrl)
            .NotEmpty().WithMessage("Image URL is required.")
            .Must(BeValidUrl).WithMessage("Image URL must be a valid URL.");
    }

    private static bool BeValidUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        return Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
