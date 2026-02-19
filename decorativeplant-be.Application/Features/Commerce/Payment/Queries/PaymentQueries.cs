using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Payment.Queries;

public class GetPaymentsByOrderQuery : IRequest<List<PaymentResponse>> { public Guid OrderId { get; set; } }
public class GetPaymentByIdQuery : IRequest<PaymentResponse?> { public Guid Id { get; set; } }
