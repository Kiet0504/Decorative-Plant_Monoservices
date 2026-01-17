using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;
using System.Security.Claims;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenResponse>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserAccountService userAccountService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // Get userId from refresh token using Redis lookup
        var userIdString = await _refreshTokenService.GetUserIdFromTokenAsync(request.RefreshToken);
        if (string.IsNullOrEmpty(userIdString))
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        if (!Guid.TryParse(userIdString, out var userId))
        {
            throw new UnauthorizedException("Invalid user ID format.");
        }

        // Validate the refresh token exists and belongs to the user
        var isValid = await _refreshTokenService.ValidateRefreshTokenAsync(userIdString, request.RefreshToken);
        if (!isValid)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        // Get user account
        var (userAccount, userProfile) = await _userAccountService.GetUserWithProfileAsync(userId, cancellationToken);
        if (!userAccount.IsActive)
        {
            throw new UnauthorizedException("User account is inactive.");
        }

        // Revoke the old refresh token (one-time use)
        await _refreshTokenService.RevokeRefreshTokenAsync(userIdString, request.RefreshToken);

        // Generate new tokens
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userAccount.Id.ToString()),
            new Claim(ClaimTypes.Email, userAccount.Email),
            new Claim(ClaimTypes.Role, userAccount.Role)
        };

        if (userProfile != null && !string.IsNullOrWhiteSpace(userProfile.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, userProfile.DisplayName));
        }

        var accessToken = _jwtService.GenerateAccessToken(claims);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        // Store new refresh token in Redis
        var expiration = _jwtService.GetRefreshTokenExpiration() - DateTime.UtcNow;
        await _refreshTokenService.StoreRefreshTokenAsync(userAccount.Id.ToString(), newRefreshToken, expiration);

        _logger.LogInformation("Refreshed tokens for user {UserId}", userAccount.Id);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }
}
