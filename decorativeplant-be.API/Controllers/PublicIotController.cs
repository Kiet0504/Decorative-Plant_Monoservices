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
    public async Task<ActionResult<ApiResponse<string>>> ExecuteAction(
        [FromQuery] Guid deviceId, 
        [FromQuery] string action, 
        [FromQuery] string token)
    {
        var device = await _context.IotDevices
            .FirstOrDefaultAsync(d => d.Id == deviceId);

        if (device == null)
            return NotFound(ApiResponse<string>.ErrorResponse("Device not found"));

        // Validate Token: SHA256(deviceId + action + secretKey)
        var secretKey = _configuration["ApiSettings:SecretKey"] ?? "default_secret";
        var rawData = $"{deviceId}{action}{device.SecretKey}{secretKey}";
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(rawData));
            var computedToken = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

            if (token != computedToken)
                return Unauthorized(ApiResponse<string>.ErrorResponse("Invalid or expired token"));
        }

        // Execute Action
        if (action == "water_now")
        {
            // Send MQTT command to the device
            await _mqttService.PublishCommandAsync(device.SecretKey, "water_now", new { duration = 30 }, default);
            return Ok(ApiResponse<string>.SuccessResponse("Success", "Command 'Water Now' sent to device successfully."));
        }

        return BadRequest(ApiResponse<string>.ErrorResponse("Unknown action"));
    }
}
