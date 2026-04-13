using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Unit>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IPasswordService _passwordService;
    private readonly IRefreshTokenService _refreshTokenService;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUserAccountService userAccountService,
        IPasswordService passwordService,
        IRefreshTokenService refreshTokenService,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _passwordService = passwordService;
        _refreshTokenService = refreshTokenService;
        _logger = logger;
    }

    public async Task<Unit> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _userAccountService.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            throw new NotFoundException("User account not found.");

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            throw new ValidationException(
                "This account does not have a password. Sign in with Google, or use Forgot password to set one.");
        }

        var valid = await _userAccountService.ValidatePasswordAsync(user, request.CurrentPassword, cancellationToken);
        if (!valid)
            throw new UnauthorizedException("Current password is incorrect.");

        var newHash = _passwordService.HashPassword(request.NewPassword);
        await _userAccountService.UpdatePasswordAsync(user.Id, newHash, cancellationToken);
        await _refreshTokenService.RevokeAllUserTokensAsync(user.Id.ToString());

        _logger.LogInformation("User {UserId} changed password.", user.Id);
        return Unit.Value;
    }
}
