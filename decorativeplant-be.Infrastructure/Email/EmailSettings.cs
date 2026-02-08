namespace decorativeplant_be.Infrastructure.Email;

/// <summary>
/// SMTP and sender settings for the email service. Add more sections (e.g. SendGridApiKey) when adding another provider.
/// </summary>
public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpHost { get; set; } = "smtp.gmail.com";
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? SmtpUser { get; set; }
    public string? SmtpPassword { get; set; }
    public string FromAddress { get; set; } = "noreply@example.com";
    public string FromName { get; set; } = "Decorative Plant";
    /// <summary>If true, emails are not sent (e.g. dev); callers can still run without errors.</summary>
    public bool DisableSending { get; set; } = false;
    /// <summary>Base folder for template files (e.g. "EmailTemplates"). Resolved relative to content root when possible.</summary>
    public string? TemplateBasePath { get; set; }
}
