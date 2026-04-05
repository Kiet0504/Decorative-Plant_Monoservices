using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Payment.Commands;

public class CreatePaymentCommand : IRequest<PaymentResponse> { public Guid UserId { get; set; } public CreatePaymentRequest Request { get; set; } = null!; }
public class HandlePayOSWebhookCommand : IRequest<bool> { public PayOSWebhookRequest Webhook { get; set; } = null!; }
public class SyncPaymentCommand : IRequest<bool> { public Guid OrderId { get; set; } }
