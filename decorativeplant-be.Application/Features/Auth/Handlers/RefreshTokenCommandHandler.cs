using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using System.Security.Claims;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, TokenResponse>
{
    private readonly UserManager<User> _userManager;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        UserManager<User> userManager,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        // Get userId from refresh token using Redis lookup
        var userId = await _refreshTokenService.GetUserIdFromTokenAsync(request.RefreshToken);
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedException("Invalid or expired refresh token.");
        }

        // Validate the refresh token exists and belongs to the user
        var isValid = await _refreshTokenService.ValidateRefreshTokenAsync(userId, request.RefreshToken);
        if (!isValid)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("User not found or inactive.");
        }

        // Revoke the old refresh token (one-time use)
        await _refreshTokenService.RevokeRefreshTokenAsync(userId, request.RefreshToken);

        // Generate new tokens
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.Name, user.UserName ?? string.Empty)
        };

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var accessToken = _jwtService.GenerateAccessToken(claims);
        var newRefreshToken = _jwtService.GenerateRefreshToken();

        // Store new refresh token in Redis
        var expiration = _jwtService.GetRefreshTokenExpiration() - DateTime.UtcNow;
        await _refreshTokenService.StoreRefreshTokenAsync(user.Id, newRefreshToken, expiration);

        _logger.LogInformation("Refreshed tokens for user {UserId}", user.Id);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }
}
