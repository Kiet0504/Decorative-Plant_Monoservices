using MediatR;
using Microsoft.Extensions.Logging;
using decorativeplant_be.Application.Common.Exceptions;
using decorativeplant_be.Application.Features.Auth.Commands;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Application.Features.Auth.Handlers;

public class SendRegistrationOtpCommandHandler : IRequestHandler<SendRegistrationOtpCommand, Unit>
{
    private readonly IUserAccountService _userAccountService;
    private readonly IOtpService _otpService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly ILogger<SendRegistrationOtpCommandHandler> _logger;

    public SendRegistrationOtpCommandHandler(
        IUserAccountService userAccountService,
        IOtpService otpService,
        IEmailTemplateService emailTemplateService,
        ILogger<SendRegistrationOtpCommandHandler> logger)
    {
        _userAccountService = userAccountService;
        _otpService = otpService;
        _emailTemplateService = emailTemplateService;
        _logger = logger;
    }

    public async Task<Unit> Handle(SendRegistrationOtpCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var existing = await _userAccountService.FindByEmailAsync(email, cancellationToken);
        if (existing != null)
            throw new ValidationException("Email is already registered.");

        const int expiresInMinutes = 10;
        var code = await _otpService.CreateOtpAsync(email, "Registration", expiresInMinutes, cancellationToken);

        var model = new Dictionary<string, string>
        {
            ["Code"] = code,
            ["ExpiresInMinutes"] = expiresInMinutes.ToString()
        };
        await _emailTemplateService.SendTemplateAsync(
            "RegistrationOtp",
            model,
            email,
            "Verify your email for registration",
            cancellationToken: cancellationToken);

        _logger.LogInformation("Registration OTP sent to {Email}", email);
        return Unit.Value;
    }
}
