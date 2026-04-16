using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.Returns.Commands;
using decorativeplant_be.Application.Features.Commerce.Returns.Queries;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/returns")]
[Authorize]
public class ReturnsController : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReturnRequestRequest request)
    {
        var userId = GetUserId() ?? throw new UnauthorizedAccessException();
        var result = await Mediator.Send(new CreateReturnRequestCommand { UserId = userId, Request = request });
        return CreatedAtAction(nameof(GetById), new { id = result.Id },
            ApiResponse<ReturnRequestResponse>.SuccessResponse(result, "Return request created", 201));
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMine([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var userId = GetUserId() ?? throw new UnauthorizedAccessException();
        var result = await Mediator.Send(new GetMyReturnsQuery { UserId = userId, Page = page, PageSize = pageSize });
        return Ok(ApiResponse<PagedResult<ReturnRequestResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetReturnByIdQuery { Id = id });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Not found", statusCode: 404));
        return Ok(ApiResponse<ReturnRequestResponse>.SuccessResponse(result));
    }

    [HttpGet]
    [Authorize(Roles = "admin,staff")]
    public async Task<IActionResult> GetAll([FromQuery] string? status = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetAllReturnsQuery { Status = status, Page = page, PageSize = pageSize });
        return Ok(ApiResponse<PagedResult<ReturnRequestResponse>>.SuccessResponse(result));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "admin,staff")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateReturnStatusRequest request)
    {
        var result = await Mediator.Send(new UpdateReturnStatusCommand
        {
            Id = id,
            ActorUserId = GetUserId(),
            Request = request,
        });
        return Ok(ApiResponse<ReturnRequestResponse>.SuccessResponse(result));
    }
}
