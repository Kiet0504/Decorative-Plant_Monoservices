using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, ApiResponse<bool>>
{
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(
        IRefreshTokenService refreshTokenService,
        ILogger<LogoutCommandHandler> logger)
    {
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<ApiResponse<bool>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrEmpty(request.RefreshToken))
            {
                // Revoke specific refresh token
                await _refreshTokenService.RevokeRefreshTokenAsync(request.UserId, request.RefreshToken);
                _logger.LogInformation("User {UserId} logged out - revoked refresh token", request.UserId);
            }
            else
            {
                // Revoke all refresh tokens for the user
                await _refreshTokenService.RevokeAllUserTokensAsync(request.UserId);
                _logger.LogInformation("User {UserId} logged out - revoked all tokens", request.UserId);
            }

            return ApiResponse<bool>.SuccessResponse(true, "Logged out successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during logout for user {UserId}", request.UserId);
            return ApiResponse<bool>.ErrorResponse("An error occurred during logout");
        }
    }
}
