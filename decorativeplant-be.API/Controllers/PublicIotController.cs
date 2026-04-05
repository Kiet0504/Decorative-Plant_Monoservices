using decorativeplant_be.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text;

namespace decorativeplant_be.API.Controllers;

/// <summary>
/// Publicly accessible endpoints for IoT actions (e.g. from email links).
/// Uses token-based validation to ensure security without requiring user login sessions.
/// </summary>
[ApiController]
[Route("api/public/iot")]
[AllowAnonymous]
public class PublicIotController : ControllerBase
{
    private readonly IIotRepository _iotRepository;
    private readonly IMqttService _mqttService;

    public PublicIotController(IIotRepository iotRepository, IMqttService mqttService)
    {
        _iotRepository = iotRepository;
        _mqttService = mqttService;
    }

    /// <summary>
    /// Executes a remote command on a device. Validates the signature to prevent unauthorized access.
    /// </summary>
    [HttpGet("execute-action")]
    public async Task<IActionResult> ExecuteAction([FromQuery] Guid deviceId, [FromQuery] string action, [FromQuery] string token)
    {
        var device = await _iotRepository.GetIotDeviceByIdAsync(deviceId, default);
        if (device == null)
        {
            return NotFound("Device not found.");
        }

        // Token Validation: HMAC-like check using Device ID, Action Name, and Device Secret Key
        var input = device.Id.ToString() + action + device.SecretKey;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        var expectedToken = Convert.ToHexString(hash).ToLower();

        if (token.ToLower() != expectedToken)
        {
            return Unauthorized("Invalid or expired action token. Access denied.");
        }

        if (action == "water_now")
        {
            // Trigger 30 seconds of watering via MQTT
            await _mqttService.PublishCommandAsync(device.SecretKey, "water_now", new { duration = 30 }, default);
            
            // Return a premium confirmation page
            return Content($@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <title>Action Successful</title>
    <style>
        body {{ font-family: 'Segoe UI', system-ui, sans-serif; background: #f0f9f6; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }}
        .card {{ background: white; padding: 60px; border-radius: 40px; text-align: center; box-shadow: 0 20px 60px rgba(0,0,0,0.05); max-width: 400px; border: 1px solid #e0f2f1; }}
        .icon {{ font-size: 64px; margin-bottom: 24px; display: block; }}
        h1 {{ color: #1a3a32; font-weight: 900; margin: 0 0 16px; letter-spacing: -1px; }}
        p {{ color: #6b7280; line-height: 1.6; margin: 0 0 32px; font-weight: 500; }}
        .status-badge {{ background: #e7f6ef; color: #059669; padding: 8px 16px; border-radius: 100px; font-size: 12px; font-weight: 900; text-transform: uppercase; letter-spacing: 1px; }}
    </style>
</head>
<body>
    <div class='card'>
        <span class='icon'>🌿💧</span>
        <div style='margin-bottom: 20px;'><span class='status-badge'>Command Sent</span></div>
        <h1>Action Successful!</h1>
        <p>The remote watering command has been transmitted to your device. Irrigation will start immediately for 30 seconds.</p>
        <p style='font-size: 12px; color: #9ca3af;'>Redirecting to alerts dashboard...</p>
        <script>
            setTimeout(function() {{
                window.location.href = '/cultivation/alerts';
            }}, 3000);
        </script>
    </div>
</body>
</html>
", "text/html");
        }

        return BadRequest("The requested action is not supported by this device.");
    }
}
