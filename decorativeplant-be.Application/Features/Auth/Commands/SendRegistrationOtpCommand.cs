using MediatR;

namespace decorativeplant_be.Application.Features.Auth.Commands;

public class SendRegistrationOtpCommand : IRequest<Unit>
{
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}
