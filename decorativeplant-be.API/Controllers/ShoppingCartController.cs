using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.ShoppingCart.Commands;
using decorativeplant_be.Application.Features.Commerce.ShoppingCart.Queries;
using Microsoft.AspNetCore.RateLimiting;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/cart")]
[Authorize]
[EnableRateLimiting("CartAndOrderPolicy")]
public class ShoppingCartController : BaseController
{
    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

    [HttpGet]
    public async Task<IActionResult> GetCart()
    {
        var result = await Mediator.Send(new GetCartQuery { UserId = GetUserId() });
        return Ok(ApiResponse<ShoppingCartResponse>.SuccessResponse(result));
    }

    [HttpPost("items")]
    public async Task<IActionResult> AddItem([FromBody] AddToCartRequest request)
    {
        var result = await Mediator.Send(new AddToCartCommand { UserId = GetUserId(), Request = request });
        return Ok(ApiResponse<ShoppingCartResponse>.SuccessResponse(result));
    }

    [HttpPut("items/{listingId:guid}")]
    public async Task<IActionResult> UpdateItem(Guid listingId, [FromBody] UpdateCartItemRequest request)
    {
        var result = await Mediator.Send(new UpdateCartItemCommand { UserId = GetUserId(), ListingId = listingId, Request = request });
        return Ok(ApiResponse<ShoppingCartResponse>.SuccessResponse(result));
    }

    [HttpDelete("items/{listingId:guid}")]
    public async Task<IActionResult> RemoveItem(Guid listingId)
    {
        var result = await Mediator.Send(new RemoveCartItemCommand { UserId = GetUserId(), ListingId = listingId });
        return Ok(ApiResponse<ShoppingCartResponse>.SuccessResponse(result));
    }

    [HttpDelete]
    public async Task<IActionResult> ClearCart()
    {
        await Mediator.Send(new ClearCartCommand { UserId = GetUserId() });
        return Ok(ApiResponse<bool>.SuccessResponse(true, "Cart cleared"));
    }
}
