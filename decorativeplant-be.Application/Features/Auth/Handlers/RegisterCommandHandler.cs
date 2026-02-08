using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;
using System.Security.Claims;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, TokenResponse>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IPasswordService _passwordService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserAccountService userAccountService,
        IPasswordService passwordService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        ILogger<RegisterCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _passwordService = passwordService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // Validate password confirmation
        if (request.Password != request.ConfirmPassword)
        {
            throw new ValidationException("Password and confirmation password do not match.");
        }

        // Check if email already exists
        var existingUser = await _userAccountService.FindByEmailAsync(request.Email, cancellationToken);
        if (existingUser != null)
        {
            throw new ValidationException("Email is already registered.");
        }

        // Hash password
        var passwordHash = _passwordService.HashPassword(request.Password);

        var displayName = string.IsNullOrWhiteSpace(request.FirstName) && string.IsNullOrWhiteSpace(request.LastName)
            ? null
            : $"{request.FirstName} {request.LastName}".Trim();

        // Create user account with default role "customer" (new schema)
        var userAccount = await _userAccountService.CreateUserAccountAsync(
            email: request.Email,
            passwordHash: passwordHash,
            phone: null,
            role: "customer",
            displayName: displayName,
            cancellationToken: cancellationToken);

        // Generate JWT claims
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userAccount.Id.ToString()),
            new Claim(ClaimTypes.Email, userAccount.Email),
            new Claim(ClaimTypes.Role, userAccount.Role)
        };

        if (!string.IsNullOrWhiteSpace(userAccount.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.Name, userAccount.DisplayName));
        }

        var accessToken = _jwtService.GenerateAccessToken(claims);
        var refreshToken = _jwtService.GenerateRefreshToken();

        // Store refresh token in Redis
        var expiration = _jwtService.GetRefreshTokenExpiration() - DateTime.UtcNow;
        await _refreshTokenService.StoreRefreshTokenAsync(userAccount.Id.ToString(), refreshToken, expiration);

        _logger.LogInformation("User {UserId} registered successfully", userAccount.Id);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }
}
