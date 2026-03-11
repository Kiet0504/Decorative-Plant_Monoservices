using FluentValidation;

namespace decorativeplant_be.Application.Features.IoT.Commands.CreateIotDevice;

public class CreateIotDeviceCommandValidator : AbstractValidator<CreateIotDeviceCommand>
{
    public CreateIotDeviceCommandValidator()
    {
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
