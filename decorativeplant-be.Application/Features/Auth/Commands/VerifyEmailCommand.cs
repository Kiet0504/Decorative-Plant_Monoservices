using MediatR;
using decorativeplant_be.Application.Common.DTOs.Auth;

namespace decorativeplant_be.Application.Features.Auth.Commands;

public class VerifyEmailCommand : IRequest<TokenResponse>
{
    public string Email { get; set; } = string.Empty;
    public string Otp { get; set; } = string.Empty;
}
