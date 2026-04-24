using decorativeplant_be.Application.DTOs.IoT;
using decorativeplant_be.Application.Features.IoT.Queries;
using decorativeplant_be.Application.Features.IoT.Commands;
using decorativeplant_be.Application.Features.IoT.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/iot/automation-rules")]
[Authorize]
public class AutomationRulesController : BaseController
{
    /// <summary>Get all automation rules. Filter by deviceId and branchId.</summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<AutomationRuleDto>>> GetRules([FromQuery] Guid? deviceId, [FromQuery] Guid? branchId)
    {
        var result = await Mediator.Send(new GetAutomationRulesQuery { DeviceId = deviceId, BranchId = branchId });
        return Ok(result);
    }

    /// <summary>Get a single automation rule by ID.</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<AutomationRuleDto>> GetRuleById(Guid id)
    {
        var result = await Mediator.Send(new GetAutomationRuleByIdQuery { RuleId = id });
        if (result == null) return NotFound();
        return Ok(result);
    }

    /// <summary>Create a new automation rule.</summary>
    [HttpPost]
    public async Task<ActionResult<AutomationRuleDto>> Create([FromBody] CreateAutomationRuleDto dto)
    {
        var result = await Mediator.Send(new CreateAutomationRuleCommand { Dto = dto });
        return CreatedAtAction(nameof(GetRuleById), new { id = result.Id }, result);
    }

    /// <summary>Update an existing automation rule.</summary>
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(Guid id, [FromBody] UpdateAutomationRuleDto dto)
    {
        var ok = await Mediator.Send(new UpdateAutomationRuleCommand { RuleId = id, Dto = dto });
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>Delete an automation rule.</summary>
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(Guid id)
    {
        var ok = await Mediator.Send(new DeleteAutomationRuleCommand { RuleId = id });
        if (!ok) return NotFound();
        return NoContent();
    }

    /// <summary>Get suggested conditions based on plants at the device's location.</summary>
    [HttpGet("suggestion/{deviceId}")]
    public async Task<ActionResult<IEnumerable<AutomationSuggestionDto>>> GetSuggestions(
        Guid deviceId, 
        [FromQuery] string? growthStage, 
        [FromQuery] string? season)
    {
        var result = await Mediator.Send(new GetAutomationSuggestionQuery 
        { 
            DeviceId = deviceId,
            GrowthStage = growthStage,
            Season = season
        });
        return Ok(result);
    }
}
