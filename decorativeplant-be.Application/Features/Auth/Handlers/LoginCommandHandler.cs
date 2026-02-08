using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;
using System.Security.Claims;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class LoginCommandHandler : IRequestHandler<LoginCommand, TokenResponse>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserAccountService userAccountService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        ILogger<LoginCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var userAccount = await _userAccountService.FindByEmailAsync(request.Email, cancellationToken);
        if (userAccount == null || !userAccount.IsActive)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var isValidPassword = await _userAccountService.ValidatePasswordAsync(userAccount, request.Password, cancellationToken);
        if (!isValidPassword)
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var user = await _userAccountService.GetByIdAsync(userAccount.Id, cancellationToken)
            ?? userAccount;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role)
        };

        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, user.DisplayName));
        }

        var accessToken = _jwtService.GenerateAccessToken(claims);
        var refreshToken = _jwtService.GenerateRefreshToken();

        var expiration = _jwtService.GetRefreshTokenExpiration() - DateTime.UtcNow;
        await _refreshTokenService.StoreRefreshTokenAsync(user.Id.ToString(), refreshToken, expiration);

        _logger.LogInformation("User {UserId} logged in successfully", user.Id);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30) // Access token expiration
        };
    }
}
