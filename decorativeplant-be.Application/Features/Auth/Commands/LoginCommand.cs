using decorativeplant_be.Application.Common.DTOs.Auth;
using MediatR;

namespace decorativeplant_be.Application.Features.Auth.Commands;

public class LoginCommand : IRequest<TokenResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
