using Microsoft.AspNetCore.Mvc;
using MediatR;
using decorativeplant_be.Application.Features.Revenue.Queries;
using Microsoft.AspNetCore.Authorization;

namespace decorativeplant_be.API.Controllers;

[Authorize(Roles = "admin")]
[ApiController]
[Route("api/[controller]")]
public class RevenueController : ControllerBase
{
    private readonly IMediator _mediator;

    public RevenueController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? branchId)
    {
        var result = await _mediator.Send(new GetRevenueSummaryQuery(from, to, branchId));
        return Ok(result);
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> GetMonthly([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? branchId)
    {
        var result = await _mediator.Send(new GetMonthlyRevenueQuery(from, to, branchId));
        return Ok(result);
    }

    [HttpGet("branches")]
    public async Task<IActionResult> GetBranches([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? branchId)
    {
        var result = await _mediator.Send(new GetBranchRevenueQuery(from, to, branchId));
        return Ok(result);
    }

    [HttpGet("top-products")]
    public async Task<IActionResult> GetTopProducts([FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] Guid? branchId, [FromQuery] int topCount = 10)
    {
        var result = await _mediator.Send(new GetTopProductRevenueQuery(topCount, from, to, branchId));
        return Ok(result);
    }
}
