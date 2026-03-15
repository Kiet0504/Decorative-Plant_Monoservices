// decorativeplant-be.Application/Features/Company/Commands/CreateCompanyCommandValidator.cs

using FluentValidation;

namespace decorativeplant_be.Application.Features.Company.Commands;

public class CreateCompanyCommandValidator : AbstractValidator<CreateCompanyCommand>
{
    public CreateCompanyCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("Email must be a valid email address.");
    }
}
