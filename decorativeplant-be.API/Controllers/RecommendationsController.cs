using System.Security.Claims;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Common.DTOs.Recommendations;
using decorativeplant_be.Application.Features.Recommendations.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace decorativeplant_be.API.Controllers;

[ApiController]
[Route("api/recommendations")]
[Authorize]
public class RecommendationsController : ControllerBase
{
    private IMediator? _mediator;
    private IMediator Mediator => _mediator ??= HttpContext.RequestServices.GetRequiredService<IMediator>();

    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

    [HttpPost("products")]
    public async Task<ActionResult<ApiResponse<ProductRecommendationsResponse>>> RecommendProducts([FromBody] ProductRecommendationsRequest request)
    {
        var query = new GetProductRecommendationsQuery
        {
            UserId = GetUserId(),
            Request = request ?? new ProductRecommendationsRequest()
        };

        var result = await Mediator.Send(query);
        return Ok(ApiResponse<ProductRecommendationsResponse>.SuccessResponse(result));
    }
}

