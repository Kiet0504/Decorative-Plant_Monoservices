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
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        UserManager<User> userManager,
        IJwtService jwtService,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userManager = userManager;
        _jwtService = jwtService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var principal = _jwtService.GetPrincipalFromExpiredToken(request.RefreshToken);
        if (principal == null)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            throw new UnauthorizedException("Invalid token claims.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("User not found or inactive.");
        }

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
        var refreshToken = _jwtService.GenerateRefreshToken();

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }
}
