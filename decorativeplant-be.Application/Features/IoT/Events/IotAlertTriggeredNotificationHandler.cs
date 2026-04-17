using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.IoT.Events;

public class IotAlertTriggeredNotificationHandler : INotificationHandler<IotAlertTriggeredNotification>
{
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context;
    private readonly ILogger<IotAlertTriggeredNotificationHandler> _logger;

    public IotAlertTriggeredNotificationHandler(
        IEmailTemplateService emailTemplateService, 
        IConfiguration configuration,
        IApplicationDbContext context,
        ILogger<IotAlertTriggeredNotificationHandler> logger)
    {
        _emailTemplateService = emailTemplateService;
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    public async Task Handle(IotAlertTriggeredNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var device = notification.Device;
            var alert = notification.Alert;
            
            _logger.LogInformation("Processing IoT Alert Notification for Device {DeviceId}", device.Id);

            // 1. Find Current Cultivation Staff Emails
            var staffEmails = await _context.UserAccounts
                .AsNoTracking()
                .Where(u => u.Role == "cultivation_staff" && u.IsActive)
                .Select(u => u.Email)
                .ToListAsync(cancellationToken);

            if (!staffEmails.Any())
            {
                _logger.LogWarning("No active cultivation staff found to receive IoT alert {AlertId}", alert.Id);
                return;
            }

            // 2. Extract Alert Details from JSONB alert_info
            var alertMessage = "Abnormal behavior detected!";
            var severity = "WARNING";
            
            if (alert.AlertInfo != null)
            {
                var root = alert.AlertInfo.RootElement;
                var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                severity = root.TryGetProperty("severity", out var s) ? s.GetString()?.ToUpper() ?? "WARNING" : "WARNING";

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(msg))
                    alertMessage = $"{title}: {msg}";
                else
                    alertMessage = title ?? msg ?? alertMessage;
            }

            // 3. Extract Device Name
            var deviceName = "Unknown Device";
            if (device.DeviceInfo != null)
            {
                if (device.DeviceInfo.RootElement.TryGetProperty("name", out var n))
                {
                    deviceName = n.GetString() ?? deviceName;
                }
            }

            // 4. Generate Security Token for Action
            const string action = "water_now";
            var apiSecretKey = _configuration["ApiSettings:SecretKey"] ?? "decorative_plant_default_secret_2024";
            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:8080";
            
            var rawData = $"{device.Id}{action}{device.SecretKey}{apiSecretKey}";
            string computedToken;
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                computedToken = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }

            var placeholders = new Dictionary<string, string>
            {
                { "Severity", severity },
                { "AlertMessage", alertMessage },
                { "ActionUrl", $"{baseUrl}/api/public/iot/action?deviceId={device.Id}&action={action}&token={computedToken}" },
                { "ActionToken", computedToken }
            };

            // 5. Dispatch Emails
            foreach (var email in staffEmails)
            {
                await _emailTemplateService.SendTemplateAsync(
                    templateName: "IotAlert",
                    model: placeholders,
                    to: email,
                    subject: $"[IoT ALERT] {severity} - {deviceName}",
                    cancellationToken: cancellationToken
                );
            }

            _logger.LogInformation("IoT Alert Email dispatched to {Count} staff members.", staffEmails.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process IotAlertTriggeredNotification.");
        }
    }
}
