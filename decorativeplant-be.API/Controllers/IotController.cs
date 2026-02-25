using decorativeplant_be.Application.Features.IoT.Commands.IngestSensorData;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class IotController : BaseController
{
    [HttpPost("sensors/ingest")]
    public async Task<IActionResult> IngestSensorData([FromBody] IngestSensorDataRequest request)
    {
        if (!Request.Headers.TryGetValue("x-device-secret", out var secretKey))
        {
            return Unauthorized(new { message = "Missing x-device-secret header" });
        }

        var command = new IngestSensorDataCommand
        {
            DeviceSecret = secretKey.ToString(),
            ComponentKey = request.ComponentKey,
            Value = request.Value
        };

        var result = await Mediator.Send(command);
        return Ok(new { success = result });
    }
}

public class IngestSensorDataRequest
{
    public string ComponentKey { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
