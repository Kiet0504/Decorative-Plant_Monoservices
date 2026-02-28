using FluentValidation;
using decorativeplant_be.Application.Features.Commerce.Vouchers.Commands;

namespace decorativeplant_be.Application.Features.Commerce.Vouchers.Validators;

public class CreateVoucherCommandValidator : AbstractValidator<CreateVoucherCommand>
{
    public CreateVoucherCommandValidator()
    {
        RuleFor(x => x.Request.Code)
            .NotEmpty().WithMessage("Voucher code is required.")
            .MaximumLength(50).WithMessage("Voucher code cannot exceed 50 characters.");

        RuleFor(x => x.Request.Name)
            .NotEmpty().WithMessage("Voucher name is required.")
            .MaximumLength(100).WithMessage("Voucher name cannot exceed 100 characters.");

        RuleFor(x => x.Request.Type)
            .NotEmpty()
            .Must(t => t == "percentage" || t == "fixed_amount" || t == "free_shipping")
            .WithMessage("Invalid voucher type.");

        RuleFor(x => x.Request.Value)
            .NotEmpty().WithMessage("Value is required.")
            .Must(v => decimal.TryParse(v, out var val) && val >= 0)
            .WithMessage("Value must be a valid non-negative number.");

        RuleFor(x => x.Request.MinOrder)
            .Must(v => v == null || (decimal.TryParse(v, out var val) && val >= 0))
            .WithMessage("MinOrder must be a valid non-negative number.")
            .When(x => x.Request.MinOrder != null);
    }
}

public class UpdateVoucherCommandValidator : AbstractValidator<UpdateVoucherCommand>
{
    public UpdateVoucherCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        
        RuleFor(x => x.Request.Name)
            .MaximumLength(100).WithMessage("Voucher name cannot exceed 100 characters.")
            .When(x => x.Request.Name != null);

        RuleFor(x => x.Request.Value)
            .Must(v => decimal.TryParse(v, out var val) && val >= 0)
            .WithMessage("Value must be a valid non-negative number.")
            .When(x => x.Request.Value != null);
    }
}
