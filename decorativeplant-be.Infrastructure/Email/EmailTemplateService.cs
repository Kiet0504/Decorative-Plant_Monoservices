using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using decorativeplant_be.Application.Common.DTOs.Email;
using decorativeplant_be.Application.Services;

namespace decorativeplant_be.Infrastructure.Email;

/// <summary>
/// Loads HTML (and optional .txt) templates from disk, replaces {{Placeholder}} with model values, and sends via IEmailService.
/// Keeps large or styled email content in template files instead of code.
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    private readonly IEmailService _emailService;
    private readonly EmailSettings _settings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(
        IEmailService emailService,
        IOptions<EmailSettings> settings,
        IHostEnvironment hostEnvironment,
        ILogger<EmailTemplateService> logger)
    {
        _emailService = emailService;
        _settings = settings.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task SendTemplateAsync(
        string templateName,
        IReadOnlyDictionary<string, string> model,
        string to,
        string subject,
        string? toName = null,
        CancellationToken cancellationToken = default)
    {
        var basePath = ResolveTemplateBasePath();
        var htmlPath = Path.Combine(basePath, $"{templateName}.html");
        var txtPath = Path.Combine(basePath, $"{templateName}.txt");

        var bodyHtml = await ReadAndRenderAsync(htmlPath, model, cancellationToken);
        var bodyPlainText = File.Exists(txtPath)
            ? await ReadAndRenderAsync(txtPath, model, cancellationToken)
            : StripHtml(bodyHtml);

        if (string.IsNullOrWhiteSpace(bodyHtml) && string.IsNullOrWhiteSpace(bodyPlainText))
        {
            _logger.LogWarning("Template {TemplateName} produced empty body; skipping send.", templateName);
            return;
        }

        var message = new EmailMessage
        {
            To = to,
            ToName = toName,
            Subject = subject,
            BodyPlainText = bodyPlainText ?? string.Empty,
            BodyHtml = string.IsNullOrWhiteSpace(bodyHtml) ? null : bodyHtml
        };
        await _emailService.SendAsync(message, cancellationToken);
    }

    private string ResolveTemplateBasePath()
    {
        var configured = _settings.TemplateBasePath?.Trim();
        if (string.IsNullOrEmpty(configured))
            configured = "EmailTemplates";

        if (Path.IsPathRooted(configured))
            return configured;

        var contentRoot = _hostEnvironment.ContentRootPath ?? AppContext.BaseDirectory;
        return Path.Combine(contentRoot, configured);
    }

    private static async Task<string> ReadAndRenderAsync(string filePath, IReadOnlyDictionary<string, string> model, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
            return string.Empty;

        var content = await File.ReadAllTextAsync(filePath, cancellationToken);
        return PlaceholderRegex.Replace(content, m =>
        {
            var key = m.Groups[1].Value;
            return model.TryGetValue(key, out var value) ? value : m.Value;
        });
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        var stripped = Regex.Replace(html, @"<[^>]+>", " ");
        return Regex.Replace(stripped, @"\s+", " ").Trim();
    }
}
