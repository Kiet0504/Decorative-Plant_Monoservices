// decorativeplant-be.Application/Features/Branch/Commands/UpdateStaffAssignmentCommandValidator.cs

using FluentValidation;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public class UpdateStaffAssignmentCommandValidator : AbstractValidator<UpdateStaffAssignmentCommand>
{
    public UpdateStaffAssignmentCommandValidator()
    {
        RuleFor(x => x.StaffAssignmentId)
            .NotEmpty().WithMessage("Staff Assignment ID is required.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(role => new[] { "branchManager", "cultivationStaff", "storeStaff", "fulfillmentStaff" }.Contains(role))
            .WithMessage("Role must be one of: branchManager, cultivationStaff, storeStaff, fulfillmentStaff.");
    }
}
