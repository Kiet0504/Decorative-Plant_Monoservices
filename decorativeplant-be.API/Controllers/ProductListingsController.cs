using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.ProductListings.Commands;
using decorativeplant_be.Application.Features.Commerce.ProductListings.Queries;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/product-listings")]
public class ProductListingsController : BaseController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid? branchId, [FromQuery] string? status, [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetProductListingsQuery { BranchId = branchId, Status = status, Search = search, Page = page, PageSize = pageSize });
        return Ok(ApiResponse<PagedResult<ProductListingResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetProductListingByIdQuery { Id = id });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Not found", statusCode: 404));
        return Ok(ApiResponse<ProductListingResponse>.SuccessResponse(result));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateProductListingRequest request)
    {
        var result = await Mediator.Send(new CreateProductListingCommand { Request = request });
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<ProductListingResponse>.SuccessResponse(result, "Created successfully", 201));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProductListingRequest request)
    {
        var result = await Mediator.Send(new UpdateProductListingCommand { Id = id, Request = request });
        return Ok(ApiResponse<ProductListingResponse>.SuccessResponse(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteProductListingCommand { Id = id });
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Deleted successfully"));
    }
}
