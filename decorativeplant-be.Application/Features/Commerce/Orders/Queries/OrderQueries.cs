using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;

namespace decorativeplant_be.Application.Features.Commerce.Orders.Queries;

public class GetOrdersQuery : IRequest<PagedResult<OrderResponse>>
{
    public Guid? UserId { get; set; }
    public Guid? BranchId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetOrderByIdQuery : IRequest<OrderResponse?>
{
    public Guid Id { get; set; }
}
