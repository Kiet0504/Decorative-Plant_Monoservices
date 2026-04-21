using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Payment.Queries;

public class GetPaymentsByOrderQuery : IRequest<List<PaymentResponse>> { public Guid OrderId { get; set; } }
public class GetPaymentByIdQuery : IRequest<PaymentResponse?> { public Guid Id { get; set; } }
/// <summary>
/// FIX #3: Query to check for expired pending refunds that haven't been completed within timeout period (30 days).
/// This should be run periodically (e.g., scheduled background job) to mark old refunds for admin attention.
/// </summary>
public class CheckExpiredRefundsQuery : IRequest<int> { }
