using MediatR;

namespace decorativeplant_be.Application.Features.Auth.Commands;

public class RequestPasswordResetCommand : IRequest<Unit>
{
    public string Email { get; set; } = string.Empty;
}
