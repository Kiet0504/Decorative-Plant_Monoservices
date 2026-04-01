using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Application.Features.IoT.Queries;
using decorativeplant_be.Application.Features.IoT.Commands;
using decorativeplant_be.Application.Features.IoT.Commands.CreateIotAlert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/iot/alerts")]
[Authorize]
public class IotAlertController : BaseController
{
    /// <summary>Get all IoT alerts. Filter by deviceId.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<IotAlertDto>>> GetAlerts([FromQuery] Guid? deviceId)
    {
        var result = await Mediator.Send(new GetIotAlertsQuery { DeviceId = deviceId });
        return Ok(result);
    }

    /// <summary>Create a new IoT alert manually.</summary>
    [HttpPost]
    public async Task<ActionResult<IotAlertDto>> CreateAlert([FromBody] CreateIotAlertCommand command)
    {
        var result = await Mediator.Send(command);
        return Ok(result);
    }

    /// <summary>Mark an IoT alert as resolved.</summary>
    [HttpPatch("{id}/resolve")]
    public async Task<ActionResult> Resolve(Guid id, [FromBody] ResolveIotAlertDto dto)
    {
        var ok = await Mediator.Send(new ResolveIotAlertCommand { AlertId = id, ResolutionInfo = dto.ResolutionInfo });
        if (!ok) return NotFound();
        return NoContent();
    }
}
