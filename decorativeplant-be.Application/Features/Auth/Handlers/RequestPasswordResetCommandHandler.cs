using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, Unit>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IOtpService _otpService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger;

    public RequestPasswordResetCommandHandler(
        IUserAccountService userAccountService,
        IOtpService otpService,
        IEmailTemplateService emailTemplateService,
        ILogger<RequestPasswordResetCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _otpService = otpService;
        _emailTemplateService = emailTemplateService;
        _logger = logger;
    }

    public async Task<Unit> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userAccountService.FindByEmailAsync(email, cancellationToken: cancellationToken);
        if (user == null)
        {
            _logger.LogWarning("Password reset requested for non-existent email {Email}", email);
            return Unit.Value;
        }

        const int expiresInMinutes = 15;
        var code = await _otpService.CreateOtpAsync(email, "PasswordReset", expiresInMinutes, cancellationToken);

        var model = new Dictionary<string, string>
        {
            ["Code"] = code,
            ["ExpiresInMinutes"] = expiresInMinutes.ToString()
        };
        await _emailTemplateService.SendTemplateAsync(
            "PasswordResetOtp",
            model,
            email,
            "Reset your password",
            cancellationToken: cancellationToken);

        _logger.LogInformation("Password reset OTP sent to {Email}", email);
        return Unit.Value;
    }
}
