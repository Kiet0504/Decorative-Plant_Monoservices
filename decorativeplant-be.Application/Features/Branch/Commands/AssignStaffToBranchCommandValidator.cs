// decorativeplant-be.Application/Features/Branch/Commands/AssignStaffToBranchCommandValidator.cs

using decorativeplant_be.Application.Common;
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
            .Must(role => StaffRoleNormalizer.BranchAssignableRoles.Contains(StaffRoleNormalizer.Normalize(role)))
            .WithMessage(
                "Role must be one of: branch_manager, store_staff, cultivation_staff, fulfillment_staff (camelCase variants are accepted).");
    }
}
