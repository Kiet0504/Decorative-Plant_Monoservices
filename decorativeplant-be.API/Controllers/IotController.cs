using Microsoft.AspNetCore.Mvc;
using MediatR;
using decorativeplant_be.Application.Features.IoT.Commands.CreateIotDevice;
using decorativeplant_be.Application.Features.IoT.Commands.DeleteIotDevice;
using decorativeplant_be.Application.Features.IoT.Commands.UpdateIotDevice;
using decorativeplant_be.Application.Features.IoT.Queries.GetIotDeviceById;
using decorativeplant_be.Application.Features.IoT.Queries.GetIotDevices;
using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Application.Common.DTOs.Common;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/Iot")]
[ApiController]
public class IotController : BaseController
{
    [HttpPost("sensors/ingest")]
    public async Task<IActionResult> IngestSensorData([FromBody] IngestSensorDataRequest request)
    {
        var result = await Mediator.Send(new decorativeplant_be.Application.Features.IoT.Commands.IngestSensorData.IngestSensorDataCommand
        {
            ComponentKey = request.ComponentKey,
            Value = request.Value
        });

        if (!result) return BadRequest(ApiResponse<object>.ErrorResponse("Failed to ingest data"));
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Data ingested successfully"));
    }

    [HttpGet("devices")]
    public async Task<IActionResult> GetDevices()
    {
        var devices = await Mediator.Send(new GetIotDevicesQuery());
        return Ok(ApiResponse<IEnumerable<IotDeviceDto>>.SuccessResponse(devices));
    }

    [HttpGet("devices/{id}")]
    public async Task<IActionResult> GetDeviceById(Guid id)
    {
        var device = await Mediator.Send(new GetIotDeviceByIdQuery(id));
        if (device == null) return NotFound(ApiResponse<object>.ErrorResponse("Device not found", statusCode: 404));
        return Ok(ApiResponse<IotDeviceDto>.SuccessResponse(device));
    }

    [HttpPost("devices")]
    public async Task<IActionResult> CreateDevice([FromBody] CreateIotDeviceDto dto)
    {
        var result = await Mediator.Send(new CreateIotDeviceCommand { Device = dto });
        return CreatedAtAction(nameof(GetDeviceById), new { id = result.Id }, ApiResponse<IotDeviceDto>.SuccessResponse(result, "Created successfully", 201));
    }

    [HttpPut("devices/{id}")]
    public async Task<IActionResult> UpdateDevice(Guid id, [FromBody] UpdateIotDeviceDto dto)
    {
        var result = await Mediator.Send(new UpdateIotDeviceCommand { Id = id, Device = dto });
        if (!result) return NotFound(ApiResponse<object>.ErrorResponse("Device not found", statusCode: 404));
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Updated successfully"));
    }

    [HttpDelete("devices/{id}")]
    public async Task<IActionResult> DeleteDevice(Guid id)
    {
        var result = await Mediator.Send(new DeleteIotDeviceCommand(id));
        if (!result) return NotFound(ApiResponse<object>.ErrorResponse("Device not found", statusCode: 404));
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Deleted successfully"));
    }

    [HttpGet("sensors/metrics")]
    public async Task<IActionResult> GetSensorMetrics(
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
        return Ok(ApiResponse<IEnumerable<SensorReadingDto>>.SuccessResponse(metrics));
    }

    [HttpGet("sensors/rules")]
    public async Task<IActionResult> GetDeviceRules()
    {
        if (!Request.Headers.TryGetValue("x-device-secret", out var secretKey))
            return Unauthorized(ApiResponse<object>.ErrorResponse("Missing x-device-secret header", statusCode: 401));

        var query = new decorativeplant_be.Application.Features.IoT.Queries.GetDeviceRulesQuery { DeviceSecret = secretKey.ToString() };
        var rules = await Mediator.Send(query);
        return Ok(ApiResponse<object>.SuccessResponse(rules));
    }

    [HttpPost("sensors/logs")]
    public async Task<IActionResult> CreateExecutionLog([FromBody] decorativeplant_be.Application.Features.IoT.Commands.CreateExecutionLogCommand request)
    {
        if (!Request.Headers.TryGetValue("x-device-secret", out var secretKey))
            return Unauthorized(ApiResponse<object>.ErrorResponse("Missing x-device-secret header", statusCode: 401));

        request.DeviceSecret = secretKey.ToString();
        var result = await Mediator.Send(request);
        return Ok(ApiResponse<bool>.SuccessResponse(result));
    }
}

public class IngestSensorDataRequest
{
    public string ComponentKey { get; set; } = string.Empty;
    public decimal Value { get; set; }
}
