using FluentValidation;
using decorativeplant_be.Application.Features.Auth.Commands;

namespace decorativeplant_be.Application.Features.Auth.Validators;

public class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty()
            .WithMessage("Current password is required.");

        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .WithMessage("New password is required.")
            .MinimumLength(6)
            .WithMessage("Password must be at least 6 characters.")
            .Matches(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)")
            .WithMessage("Password must contain at least one uppercase letter, one lowercase letter, and one number.");

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty()
            .WithMessage("Confirm password is required.")
            .Equal(x => x.NewPassword)
            .WithMessage("Password and confirmation do not match.");
    }
}
