using FluentValidation;

namespace decorativeplant_be.Application.Features.IoT.Commands.IngestSensorData;

public class IngestSensorDataCommandValidator : AbstractValidator<IngestSensorDataCommand>
{
    public IngestSensorDataCommandValidator()
    {
        RuleFor(v => v.DeviceSecret)
            .NotEmpty().WithMessage("Device secret is required.");

        RuleFor(v => v.ComponentKey)
            .NotEmpty().WithMessage("Component key is required.")
            .MaximumLength(50).WithMessage("Component key must not exceed 50 characters.");
            
        RuleFor(v => v.Value)
            .NotNull().WithMessage("Sensor value is required.");
    }
}
