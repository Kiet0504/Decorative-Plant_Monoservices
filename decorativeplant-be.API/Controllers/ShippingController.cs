using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.Shipping.Commands;
using decorativeplant_be.Application.Features.Commerce.Shipping.Queries;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/shipping")]
[Authorize]
public class ShippingController : BaseController
{
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShippingRequest request)
    {
        var result = await Mediator.Send(new CreateShippingCommand { Request = request });
        return CreatedAtAction(null, ApiResponse<ShippingResponse>.SuccessResponse(result, "Shipping created", 201));
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateShippingStatusRequest request)
    {
        var result = await Mediator.Send(new UpdateShippingStatusCommand { Id = id, Request = request });
        return Ok(ApiResponse<ShippingResponse>.SuccessResponse(result));
    }

    [HttpGet("order/{orderId:guid}")]
    public async Task<IActionResult> GetByOrder(Guid orderId)
    {
        var result = await Mediator.Send(new GetShippingByOrderQuery { OrderId = orderId });
        return Ok(ApiResponse<List<ShippingResponse>>.SuccessResponse(result));
    }
}

[Route("api/v{version:apiVersion}/shipping-zones")]
[Authorize]
public class ShippingZonesController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? branchId)
    {
        var result = await Mediator.Send(new GetShippingZonesQuery { BranchId = branchId });
        return Ok(ApiResponse<List<ShippingZoneResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetShippingZoneByIdQuery { Id = id });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Not found", statusCode: 404));
        return Ok(ApiResponse<ShippingZoneResponse>.SuccessResponse(result));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateShippingZoneRequest request)
    {
        var result = await Mediator.Send(new CreateShippingZoneCommand { Request = request });
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<ShippingZoneResponse>.SuccessResponse(result, "Created", 201));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateShippingZoneRequest request)
    {
        var result = await Mediator.Send(new UpdateShippingZoneCommand { Id = id, Request = request });
        return Ok(ApiResponse<ShippingZoneResponse>.SuccessResponse(result));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteShippingZoneCommand { Id = id });
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Deleted"));
    }
}
