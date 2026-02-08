using decorativeplant_be.Application.Common.DTOs.Email;

namespace decorativeplant_be.Application.Services;

/// <summary>
/// Abstraction for sending email. Implement with SMTP (MailKit), SendGrid, AWS SES, etc.
/// Keeps the app agnostic to provider and easy to scale or change later.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email. Implementations may use BodyHtml, TemplateId, or TemplateData as supported.
    /// </summary>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
