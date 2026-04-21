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
            var componentName = alert.ComponentKey ?? "Sensor";
            var value = "--";
            var unit = "";
            var threshold = "--";
            
            if (alert.AlertInfo != null)
            {
                var root = alert.AlertInfo.RootElement;
                var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
                var msg = root.TryGetProperty("message", out var m) ? m.GetString() : null;
                severity = root.TryGetProperty("severity", out var s) ? s.GetString()?.ToUpper() ?? "WARNING" : "WARNING";

                // New detailed fields
                if (root.TryGetProperty("componentName", out var cn)) componentName = cn.GetString() ?? componentName;
                if (root.TryGetProperty("value", out var v)) value = v.ValueKind == JsonValueKind.Number ? v.GetDecimal().ToString("F1") : v.GetString() ?? value;
                if (root.TryGetProperty("unit", out var u)) unit = u.GetString() ?? unit;
                if (root.TryGetProperty("threshold", out var th)) threshold = th.ValueKind == JsonValueKind.Number ? th.GetDecimal().ToString("F1") : th.GetString() ?? threshold;

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(msg))
                    alertMessage = $"{title}: {msg}";
                else
                    alertMessage = title ?? msg ?? alertMessage;
                
                // Fallback for component name from key if still generic
                if (componentName == "Sensor" && !string.IsNullOrEmpty(alert.ComponentKey))
                {
                    componentName = System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(alert.ComponentKey.Replace("_", " "));
                }
            }

            // 3. Extract Device Name & Location
            var deviceName = "Unknown Device";
            var locationName = device.Location?.Name ?? "General Area";

            if (device.DeviceInfo != null)
            {
                if (device.DeviceInfo.RootElement.TryGetProperty("name", out var n))
                {
                    deviceName = n.GetString() ?? deviceName;
                }
            }

            var apiSecretKey = _configuration["ApiSettings:SecretKey"] ?? "decorative_plant_default_secret_2024";
            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:8080";
            
            string? actionUrl = null;
            string? actionToken = null;

            // Simple check for water_now action (backward compatibility)
            var action = (alert.ComponentKey?.Contains("moisture") == true) ? "water_now" : null;

            if (!string.IsNullOrEmpty(action))
            {
                var rawData = $"{device.Id}{action}{device.SecretKey}{apiSecretKey}";
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                    actionToken = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
                }
                actionUrl = $"{baseUrl}/api/public/iot/action?deviceId={device.Id}&action={action}&token={actionToken}";
            }

            var placeholders = new Dictionary<string, string>
            {
                { "Severity", severity },
                { "AlertMessage", alertMessage },
                { "DeviceName", deviceName },
                { "Location", locationName },
                { "ComponentName", componentName },
                { "Value", value },
                { "Unit", unit },
                { "Threshold", threshold },
                { "Time", (alert.CreatedAt ?? DateTime.UtcNow).AddHours(7).ToString("yyyy-MM-dd HH:mm:ss") },
                { "ActionUrl", actionUrl ?? "" },
                { "ActionToken", actionToken ?? "" },
                { "HasAction", (action != null).ToString().ToLower() }
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
