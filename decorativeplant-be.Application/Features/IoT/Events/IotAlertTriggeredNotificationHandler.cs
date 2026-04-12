using decorativeplant_be.Application.Common.DTOs.Email;
using decorativeplant_be.Application.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace decorativeplant_be.Application.Features.IoT.Events;

public class IotAlertTriggeredNotificationHandler : INotificationHandler<IotAlertTriggeredNotification>
{
    private readonly IEmailService _emailService;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IotAlertTriggeredNotificationHandler> _logger;

    public IotAlertTriggeredNotificationHandler(
        IEmailService emailService, 
        IEmailTemplateService emailTemplateService, 
        IConfiguration configuration,
        ILogger<IotAlertTriggeredNotificationHandler> logger)
    {
        _emailService = emailService;
        _emailTemplateService = emailTemplateService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task Handle(IotAlertTriggeredNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            var device = notification.Device;
            var alert = notification.Alert;
            
            _logger.LogInformation("Processing email notification for IoT Alert on Device {DeviceId}", device.Id);

            // Construct readable alert message
            var alertMessage = $"Cảm biến {alert.ComponentKey} tại thiết bị {device.Id} (Kho/Vườn) đã vi phạm quy tắc: {notification.RuleName}. Vui lòng kiểm tra khẩn cấp!";

            // Extract the API Host or build ActionUrl for the email buttons
            var apiBaseUrl = _configuration["ApiSettings:BaseUrl"] ?? "http://localhost:5000";
            var actionUrl = $"{apiBaseUrl}/api/public/iot/action?deviceId={device.Id}";

            // Generate secure ActionToken for bypassing auth when clicking "water_now"
            var secretKey = _configuration["ApiSettings:SecretKey"] ?? "default_secret";
            var rawData = $"{device.Id}water_now{device.SecretKey}{secretKey}";
            var computedToken = "";
            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
                computedToken = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }

            var placeholders = new Dictionary<string, string>
            {
                { "AlertMessage", alertMessage },
                { "ActionUrl", actionUrl },
                { "ActionToken", computedToken }
            };

            var targetEmail = "decorativeplant.staff@gmail.com"; 

            await _emailTemplateService.SendTemplateAsync(
                templateName: "IotAlert",
                model: placeholders,
                to: targetEmail,
                subject: $"[CẢNH BÁO MỨC ĐỘ KHẨN] Vi phạm quy tắc {notification.RuleName} - Cảm biến {alert.ComponentKey}",
                toName: "Cultivation Staff",
                cancellationToken: cancellationToken
            );

            _logger.LogInformation("Successfully dispatched IoT Alert Email to {email}", targetEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send IoT Alert Email to Cultivation Staff.");
            // We fail silently preventing the main API thread from crashing just because email delivery fails.
        }
    }
}
