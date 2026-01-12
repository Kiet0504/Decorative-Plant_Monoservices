using decorativeplant_be.Application.Common.DTOs.Common;
using MediatR;

namespace decorativeplant_be.Application.Features.Auth.Commands;

public class LogoutCommand : IRequest<ApiResponse<bool>>
{
    public string UserId { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
}
