using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Application.Features.IoT.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/iot/execution-logs")]
[Authorize]
public class AutomationExecutionLogController : BaseController
{
    /// <summary>Get automation execution logs. Filter by ruleId, from, to.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AutomationExecutionLogDto>>> GetLogs(
        [FromQuery] Guid? ruleId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to)
    {
        var result = await Mediator.Send(new GetExecutionLogsQuery { RuleId = ruleId, From = from, To = to });
        return Ok(result);
    }
}
