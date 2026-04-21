using decorativeplant_be.Application.Common;
using FluentValidation;

namespace decorativeplant_be.Application.Features.Branch.Commands;

public class UpsertBranchStaffAccountCommandValidator : AbstractValidator<UpsertBranchStaffAccountCommand>
{
    public UpsertBranchStaffAccountCommandValidator()
    {
        RuleFor(x => x.BranchId).NotEmpty();
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Role)
            .NotEmpty()
            .Must(role => StaffRoleNormalizer.BranchAssignableRoles.Contains(StaffRoleNormalizer.Normalize(role)))
            .WithMessage(
                "Role must be one of: branch_manager, store_staff, cultivation_staff, fulfillment_staff.");
    }
}
