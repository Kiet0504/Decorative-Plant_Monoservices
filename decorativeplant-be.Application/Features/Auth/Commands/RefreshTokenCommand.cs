using decorativeplant_be.Application.Common.DTOs.Auth;
using MediatR;

namespace decorativeplant_be.Application.Features.Auth.Commands;

public class RefreshTokenCommand : IRequest<TokenResponse>
{
    public string RefreshToken { get; set; } = string.Empty;
}
