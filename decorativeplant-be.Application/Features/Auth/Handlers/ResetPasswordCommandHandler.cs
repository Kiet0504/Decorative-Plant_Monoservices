using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Unit>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IPasswordService _passwordService;
    private readonly IOtpService _otpService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IUserAccountService userAccountService,
        IPasswordService passwordService,
        IOtpService otpService,
        IRefreshTokenService refreshTokenService,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _passwordService = passwordService;
        _otpService = otpService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<Unit> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        if (request.NewPassword != request.ConfirmPassword)
            throw new ValidationException("Password and confirmation do not match.");

        var email = request.Email.Trim().ToLowerInvariant();
        var isValid = await _otpService.ValidateAndConsumeOtpAsync(email, request.Otp, "PasswordReset", cancellationToken);
        if (!isValid)
            throw new ValidationException("Invalid or expired verification code. Please request a new one.");

        var user = await _userAccountService.FindByEmailAsync(email, cancellationToken: cancellationToken);
        if (user == null)
            throw new ValidationException("Account not found.");

        var newHash = _passwordService.HashPassword(request.NewPassword);
        await _userAccountService.UpdatePasswordAsync(user.Id, newHash, cancellationToken);

        // Revoke all refresh tokens so old sessions must log in again with the new password
        await _refreshTokenService.RevokeAllUserTokensAsync(user.Id.ToString());

        _logger.LogInformation("Password reset completed for {Email}", email);
        return Unit.Value;
    }
}
