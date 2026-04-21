using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.Vouchers.Commands;
using decorativeplant_be.Application.Features.Commerce.Vouchers.Queries;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/vouchers")]
public class VouchersController : BaseController
{
    // Public listing (marketing): activeOnly is auto-forced for anonymous callers so
    // unauthenticated users can't enumerate inactive/expired vouchers.
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? branchId, [FromQuery] bool? activeOnly, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (User?.Identity?.IsAuthenticated != true) activeOnly = true;
        var result = await Mediator.Send(new GetVouchersQuery { BranchId = branchId, ActiveOnly = activeOnly, Page = page, PageSize = pageSize });
        return Ok(ApiResponse<PagedResult<VoucherResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetVoucherByIdQuery { Id = id });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Not found", statusCode: 404));
        if (User?.Identity?.IsAuthenticated != true && !result.IsActive)
            return NotFound(ApiResponse<object>.ErrorResponse("Not found", statusCode: 404));
        return Ok(ApiResponse<VoucherResponse>.SuccessResponse(result));
    }

    // Brute-force guard: voucher codes are short and guessable. Rate-limit at the
    // cart/order policy so a scraper can't enumerate the full code space.
    [HttpGet("validate/{code}")]
    [Authorize]
    [EnableRateLimiting("CartAndOrderPolicy")]
    public async Task<IActionResult> Validate(string code, [FromQuery] Guid? branchId)
    {
        var result = await Mediator.Send(new ValidateVoucherQuery { Code = code, BranchId = branchId });
        return Ok(ApiResponse<ValidateVoucherResponse>.SuccessResponse(result));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateVoucherRequest request)
    {
        var result = await Mediator.Send(new CreateVoucherCommand { Request = request });
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<VoucherResponse>.SuccessResponse(result, "Created", 201));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVoucherRequest request)
    {
        var result = await Mediator.Send(new UpdateVoucherCommand { Id = id, Request = request });
        return Ok(ApiResponse<VoucherResponse>.SuccessResponse(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteVoucherCommand { Id = id });
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Deleted"));
    }
}
