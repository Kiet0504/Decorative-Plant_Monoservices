// decorativeplant-be.Application/Features/Branch/Commands/UpdateBranchCommandValidator.cs

using FluentValidation;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public class UpdateBranchCommandValidator : AbstractValidator<UpdateBranchCommand>
{
    public UpdateBranchCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Branch ID is required.");

        // Code cannot be changed after creation, so no validation needed

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Branch name is required.");

        RuleFor(x => x.Slug)
            .NotEmpty().WithMessage("Branch slug is required.");
    }
}
