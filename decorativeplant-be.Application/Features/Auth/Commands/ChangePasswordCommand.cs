using MediatR;

namespace decorativeplant_be.Application.Features.Auth.Commands;

public class ChangePasswordCommand : IRequest<Unit>
{
    public Guid UserId { get; set; }

    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
