using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Queries;

public class GetOrdersQuery : IRequest<List<OrderResponse>>
{
    public Guid? UserId { get; set; }
    public Guid? BranchId { get; set; }
    public string? Status { get; set; }
}

public class GetOrderByIdQuery : IRequest<OrderResponse?>
{
    public Guid Id { get; set; }
}
