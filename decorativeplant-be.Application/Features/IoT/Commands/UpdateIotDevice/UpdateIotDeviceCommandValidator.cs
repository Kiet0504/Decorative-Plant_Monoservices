using FluentValidation;

namespace decorativeplant_be.Application.Features.IoT.Commands.UpdateIotDevice;

public class UpdateIotDeviceCommandValidator : AbstractValidator<UpdateIotDeviceCommand>
{
    public UpdateIotDeviceCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty()
            .WithMessage("Device ID is required.");

        RuleFor(v => v.Device)
            .NotNull()
            .WithMessage("Device information must be provided.");

        When(v => v.Device != null, () =>
        {
            RuleFor(v => v.Device.Status)
                .MaximumLength(50)
                .WithMessage("Status must not exceed 50 characters.");
        });
    }
}
