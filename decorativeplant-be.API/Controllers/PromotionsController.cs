using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.Promotions.Commands;
using decorativeplant_be.Application.Features.Commerce.Promotions.Queries;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/promotions")]
public class PromotionsController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? branchId)
    {
        var result = await Mediator.Send(new GetPromotionsQuery { BranchId = branchId });
        return Ok(ApiResponse<List<PromotionResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetPromotionByIdQuery { Id = id });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Not found", statusCode: 404));
        return Ok(ApiResponse<PromotionResponse>.SuccessResponse(result));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePromotionRequest request)
    {
        var result = await Mediator.Send(new CreatePromotionCommand { Request = request });
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<PromotionResponse>.SuccessResponse(result, "Created", 201));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePromotionRequest request)
    {
        var result = await Mediator.Send(new UpdatePromotionCommand { Id = id, Request = request });
        return Ok(ApiResponse<PromotionResponse>.SuccessResponse(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeletePromotionCommand { Id = id });
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Deleted"));
    }
}
