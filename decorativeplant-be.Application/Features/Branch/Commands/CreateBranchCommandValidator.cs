// decorativeplant-be.Application/Features/Branch/Commands/CreateBranchCommandValidator.cs

using FluentValidation;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public class CreateBranchCommandValidator : AbstractValidator<CreateBranchCommand>
{
    public CreateBranchCommandValidator()
    {
        // CompanyId and Code are auto-generated, no longer validated here

        RuleFor(x => x.CurrentUserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Branch name is required.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Branch slug is required.");
    }
}
