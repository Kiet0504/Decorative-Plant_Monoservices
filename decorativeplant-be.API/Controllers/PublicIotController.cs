using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace decorativeplant_be.API.Controllers;

/// <summary>
/// Public endpoint for IoT actions (e.g., watering from email buttons)
/// </summary>
[ApiController]
[Route("api/public/iot")]
[AllowAnonymous]
public class PublicIotController : BaseController
{
    private readonly IApplicationDbContext _context;
    private readonly IMqttService _mqttService;
    private readonly IConfiguration _configuration;

    public PublicIotController(IApplicationDbContext context, IMqttService mqttService, IConfiguration configuration)
    {
        _context = context;
        _mqttService = mqttService;
        _configuration = configuration;
    }

    /// <summary>
    /// Executes a secure action (like "Water Now") using a signed token.
    /// </summary>
    [HttpGet("action")]
    public async Task<IActionResult> ExecuteAction(
        [FromQuery] Guid deviceId, 
        [FromQuery] string action, 
        [FromQuery] string token)
    {
        var device = await _context.IotDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device == null)
            return NotFound("Device not found");

        // Validate Token: SHA256(deviceId + action + secretKey)
        var secretKey = _configuration["ApiSettings:SecretKey"] ?? "decorative_plant_default_secret_2024";
        var rawData = $"{deviceId}{action}{device.SecretKey}{secretKey}";
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            var computedToken = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            if (token != computedToken)
                return Unauthorized("Invalid or expired token");
        }

        // Execute Action
        if (action == "water_now")
        {
            // Send MQTT command to the device
            await _mqttService.PublishCommandAsync(device.SecretKey, "water_now", new { duration = 30 }, default);
            
            // Extract device name from JSONB
            var deviceName = "Your Device";
            if (device.DeviceInfo != null && device.DeviceInfo.RootElement.TryGetProperty("name", out var nameProp))
            {
                deviceName = nameProp.GetString() ?? deviceName;
            }

            // Return a nice HTML success page instead of raw JSON
            var html = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset='utf-8'>
                    <meta name='viewport' content='width=device-width, initial-scale=1'>
                    <title>Command Executed - Decorative Plant</title>
                    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;600;800&display=swap' rel='stylesheet'>
                    <style>
                        body {{ font-family: 'Inter', sans-serif; background-color: #fcfdfc; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; }}
                        .card {{ background: white; padding: 2.5rem; border-radius: 24px; box-shadow: 0 10px 40px rgba(0,0,0,0.05); text-align: center; max-width: 400px; border: 1px solid #e5e7eb; }}
                        .icon {{ font-size: 3rem; margin-bottom: 1.5rem; }}
                        h1 {{ color: #1B4332; font-weight: 800; margin-bottom: 0.5rem; font-size: 1.5rem; }}
                        p {{ color: #6b7280; font-size: 0.95rem; line-height: 1.5; margin-bottom: 2rem; }}
                        .btn {{ background-color: #2d5f4d; color: white; text-decoration: none; padding: 12px 32px; border-radius: 12px; font-weight: 600; display: inline-block; transition: all 0.2s; }}
                        .btn:hover {{ background-color: #1b4332; transform: translateY(-2px); }}
                    </style>
                </head>
                <body>
                    <div class='card'>
                        <div class='icon'>💧</div>
                        <h1>Watering in Progress</h1>
                        <p>Command 'Water Now' has been sent to <strong>{deviceName}</strong>. The device will water for 30 seconds.</p>
                        <a href='javascript:window.close()' class='btn'>Close this page</a>
                    </div>
                </body>
                </html>";
                
            return Content(html, "text/html");
        }

        return BadRequest("Unknown action");
    }
}
