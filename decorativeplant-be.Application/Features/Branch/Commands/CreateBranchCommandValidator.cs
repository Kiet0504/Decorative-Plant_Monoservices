// decorativeplant-be.Application/Features/Branch/Commands/CreateBranchCommandValidator.cs

using FluentValidation;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("Company ID is required.");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Branch code is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Branch name is required.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Branch slug is required.");
    }
}
