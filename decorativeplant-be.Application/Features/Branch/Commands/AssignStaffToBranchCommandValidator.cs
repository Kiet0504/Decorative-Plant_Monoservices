// decorativeplant-be.Application/Features/Branch/Commands/AssignStaffToBranchCommandValidator.cs

using FluentValidation;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public class AssignStaffToBranchCommandValidator : AbstractValidator<AssignStaffToBranchCommand>
{
    public AssignStaffToBranchCommandValidator()
    {
        RuleFor(x => x.StaffId)
            .NotEmpty().WithMessage("Staff ID is required.");

        RuleFor(x => x.BranchId)
            .NotEmpty().WithMessage("Branch ID is required.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => new[] { "branchManager", "cultivationStaff", "storeStaff", "fulfillmentStaff" }.Contains(role))
            .WithMessage("Role must be one of: branchManager, cultivationStaff, storeStaff, fulfillmentStaff.");
    }
}
