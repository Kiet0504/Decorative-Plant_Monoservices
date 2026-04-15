using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.Wishlist.Commands;
using decorativeplant_be.Application.Features.Commerce.Wishlist.Queries;

namespace decorativeplant_be.API.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/users/me/wishlist")]
public class WishlistController : BaseController
{
    private Guid GetUserIdStrict() =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

    [HttpGet]
    public async Task<IActionResult> GetWishlist()
    {
        var result = await Mediator.Send(new GetWishlistQuery { UserId = GetUserIdStrict() });
        return Ok(ApiResponse<List<ProductListingResponse>>.SuccessResponse(result));
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddWishlistItemRequest request)
    {
        var ok = await Mediator.Send(new AddWishlistItemCommand { UserId = GetUserIdStrict(), Request = request });
        return Ok(ApiResponse<bool>.SuccessResponse(ok, ok ? "Added to wishlist" : "No changes"));
    }

    [HttpDelete("items/{listingId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid listingId)
    {
        var removed = await Mediator.Send(new RemoveWishlistItemCommand { UserId = GetUserIdStrict(), ListingId = listingId });
        if (!removed) return NotFound(ApiResponse<bool>.ErrorResponse("Item not in wishlist", statusCode: 404));
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Removed from wishlist"));
    }
}

