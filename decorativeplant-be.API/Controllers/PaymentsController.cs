using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;
using decorativeplant_be.Application.Features.Commerce.Payment.Commands;
using decorativeplant_be.Application.Features.Commerce.Payment.Queries;

namespace decorativeplant_be.API.Controllers;

[Route("api/v{version:apiVersion}/payments")]
public class PaymentsController : BaseController
{
    private Guid GetUserId() => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException());

    /// <summary>
    /// Create a PayOS payment link for an order
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreatePayment([FromBody] CreatePaymentRequest request)
    {
        var result = await Mediator.Send(new CreatePaymentCommand { UserId = GetUserId(), Request = request });
        return Ok(ApiResponse<PaymentResponse>.SuccessResponse(result, "Payment link created"));
    }

    [HttpGet("order/{orderId:guid}")]
    [Authorize]
    public async Task<IActionResult> GetByOrder(Guid orderId)
    {
        var result = await Mediator.Send(new GetPaymentsByOrderQuery { OrderId = orderId });
        return Ok(ApiResponse<List<PaymentResponse>>.SuccessResponse(result));
    }

    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await Mediator.Send(new GetPaymentByIdQuery { Id = id });
        if (result == null) return NotFound(ApiResponse<object>.ErrorResponse("Payment not found", statusCode: 404));
        return Ok(ApiResponse<PaymentResponse>.SuccessResponse(result));
    }

    /// <summary>
    /// PayOS webhook endpoint — receives payment notifications from PayOS
    /// </summary>
    [HttpPost("payos/webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSWebhook([FromBody] PayOSWebhookRequest request)
    {
        var result = await Mediator.Send(new HandlePayOSWebhookCommand { Webhook = request });
        return Ok(new { success = result });
    }
}
