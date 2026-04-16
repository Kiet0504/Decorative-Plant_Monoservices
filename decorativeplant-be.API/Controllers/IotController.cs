using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Application.Features.IoT.Commands.CreateIotDevice;
using decorativeplant_be.Application.Features.IoT.Commands.DeleteIotDevice;
using decorativeplant_be.Application.Features.IoT.Commands.IngestSensorData;
using decorativeplant_be.Application.Features.IoT.Commands.UpdateIotDevice;
using decorativeplant_be.Application.Features.IoT.Queries.GetIotDeviceById;
using decorativeplant_be.Application.Features.IoT.Queries.GetIotDevices;
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

    [HttpGet("devices")]
    public async Task<ActionResult<IEnumerable<IotDeviceDto>>> GetDevices([FromQuery] Guid? branchId)
    {
        var devices = await Mediator.Send(new GetIotDevicesQuery { BranchId = branchId });
        return Ok(devices);
    }

    [HttpGet("devices/{id}")]
    public async Task<ActionResult<IotDeviceDto>> GetDeviceById(Guid id)
    {
        var device = await Mediator.Send(new GetIotDeviceByIdQuery(id));
        if (device == null) return NotFound();
        return Ok(device);
    }

    [HttpPost("devices")]
    public async Task<ActionResult<IotDeviceDto>> CreateDevice([FromBody] CreateIotDeviceDto dto)
    {
        var result = await Mediator.Send(new CreateIotDeviceCommand { Device = dto });
        return CreatedAtAction(nameof(GetDeviceById), new { id = result.Id }, result);
    }

    [HttpPatch("devices/{id}")]
    public async Task<ActionResult> UpdateDevice(Guid id, [FromBody] UpdateIotDeviceDto dto)
    {
        var result = await Mediator.Send(new UpdateIotDeviceCommand { Id = id, Device = dto });
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpDelete("devices/{id}")]
    public async Task<ActionResult> DeleteDevice(Guid id)
    {
        var result = await Mediator.Send(new DeleteIotDeviceCommand(id));
        if (!result) return NotFound();
        return NoContent();
    }

    [HttpGet("sensors/metrics")]
    public async Task<ActionResult<IEnumerable<SensorReadingDto>>> GetSensorMetrics(
        [FromQuery] Guid deviceId,
        [FromQuery] string? componentKey,
        [FromQuery] DateTime? startTime,
        [FromQuery] DateTime? endTime)
    {
        var query = new decorativeplant_be.Application.Features.IoT.Queries.GetSensorMetricsQuery
        {
            DeviceId = deviceId,
            ComponentKey = componentKey,
            StartTime = startTime,
            EndTime = endTime
        };
        
        var metrics = await Mediator.Send(query);
        return Ok(metrics);
    }

    [HttpGet("sensors/rules")]
    public async Task<IActionResult> GetDeviceRules()
    {
        if (!Request.Headers.TryGetValue("x-device-secret", out var secretKey))
            return Unauthorized(new { message = "Missing x-device-secret header" });

        var query = new decorativeplant_be.Application.Features.IoT.Queries.GetDeviceRulesQuery { DeviceSecret = secretKey.ToString() };
        var rules = await Mediator.Send(query);
        return Ok(rules);
    }

    [HttpPost("sensors/logs")]
    public async Task<IActionResult> CreateExecutionLog([FromBody] decorativeplant_be.Application.Features.IoT.Commands.CreateExecutionLogCommand request)
    {
        if (!Request.Headers.TryGetValue("x-device-secret", out var secretKey))
            return Unauthorized(new { message = "Missing x-device-secret header" });

        request.DeviceSecret = secretKey.ToString();
        var result = await Mediator.Send(request);
        return Ok(new { success = result });
    }
}

public class IngestSensorDataRequest
{
    public string ComponentKey { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
