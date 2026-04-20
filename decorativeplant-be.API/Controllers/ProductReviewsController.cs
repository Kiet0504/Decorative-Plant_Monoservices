using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.ProductReviews.Commands;
using decorativeplant_be.Application.Features.Commerce.ProductReviews.Queries;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/reviews")]
public class ProductReviewsController : BaseController
{
    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetAllReviewsQuery { Page = page, PageSize = pageSize });
        return Ok(ApiResponse<PagedResult<ProductReviewResponse>>.SuccessResponse(result));
    }

    [HttpGet("listing/{listingId:guid}")]
    public async Task<IActionResult> GetByListing(Guid listingId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await Mediator.Send(new GetReviewsByListingQuery { ListingId = listingId, Page = page, PageSize = pageSize });
        return Ok(ApiResponse<PagedResult<ProductReviewResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetReviewByIdQuery { Id = id });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Not found", statusCode: 404));
        return Ok(ApiResponse<ProductReviewResponse>.SuccessResponse(result));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateProductReviewRequest request)
    {
        var result = await Mediator.Send(new CreateProductReviewCommand { UserId = GetUserId(), Request = request });
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, ApiResponse<ProductReviewResponse>.SuccessResponse(result, "Review created", 201));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateReviewStatusRequest request)
    {
        var result = await Mediator.Send(new UpdateReviewStatusCommand { Id = id, Request = request });
        return Ok(ApiResponse<ProductReviewResponse>.SuccessResponse(result));
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        await Mediator.Send(new DeleteProductReviewCommand { Id = id });
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Deleted"));
    }
}
