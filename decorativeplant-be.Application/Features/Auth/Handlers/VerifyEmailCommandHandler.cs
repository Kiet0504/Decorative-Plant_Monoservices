using System.Security.Claims;
using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.DTOs.Auth;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, TokenResponse>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IOtpService _otpService;
    private readonly IJwtService _jwtService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<VerifyEmailCommandHandler> _logger;

    public VerifyEmailCommandHandler(
        IUserAccountService userAccountService,
        IOtpService otpService,
        IJwtService jwtService,
        IRefreshTokenService refreshTokenService,
        ILogger<VerifyEmailCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _otpService = otpService;
        _jwtService = jwtService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<TokenResponse> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim();
        
        // validate OTP first
        var isValid = await _otpService.ValidateAndConsumeOtpAsync(email, request.Otp, "Registration", cancellationToken);
        if (!isValid) 
             throw new ValidationException("Invalid or expired verification code.");

        // Activate user
        var userAccount = await _userAccountService.VerifyEmailAsync(email, cancellationToken);

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

        _logger.LogInformation("User {UserId} verified email and logged in.", userAccount.Id);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        };
    }
}
