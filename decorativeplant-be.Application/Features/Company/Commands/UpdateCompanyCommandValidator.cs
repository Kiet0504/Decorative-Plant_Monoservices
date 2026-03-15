// decorativeplant-be.Application/Features/Company/Commands/UpdateCompanyCommandValidator.cs

using FluentValidation;

namespace decorativeplant_be.Application.Features.Company.Commands;

public class UpdateCompanyCommandValidator : AbstractValidator<UpdateCompanyCommand>
{
    public UpdateCompanyCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Company name is required.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrEmpty(x.Email))
            .WithMessage("Email must be a valid email address.");
    }
}
