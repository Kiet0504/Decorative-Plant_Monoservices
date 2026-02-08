using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using decorativeplant_be.Application.Common.DTOs.Email;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Infrastructure.Email;

public class SmtpEmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IOptions<EmailSettings> settings, ILogger<SmtpEmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (_settings.DisableSending)
        {
            _logger.LogWarning("Email sending is disabled. Would have sent to {To}: {Subject}", message.To, message.Subject);
            return;
        }

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(_settings.FromName, _settings.FromAddress));
        mime.To.Add(new MailboxAddress(message.ToName ?? message.To, message.To));
        mime.Subject = message.Subject;

        var builder = new BodyBuilder();
        if (!string.IsNullOrWhiteSpace(message.BodyHtml))
        {
            builder.HtmlBody = message.BodyHtml;
            builder.TextBody = message.BodyPlainText;
        }
        else
        {
            builder.TextBody = message.BodyPlainText;
        }
        mime.Body = builder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();
            var secureSocketOptions = _settings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;
            await client.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(_settings.SmtpUser) && !string.IsNullOrEmpty(_settings.SmtpPassword))
            {
                await client.AuthenticateAsync(
                    new NetworkCredential(_settings.SmtpUser, _settings.SmtpPassword),
                    cancellationToken);
            }

            await client.SendAsync(mime, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            _logger.LogInformation("Email sent to {To}", message.To);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {To}", message.To);
            throw;
        }
    }
}
