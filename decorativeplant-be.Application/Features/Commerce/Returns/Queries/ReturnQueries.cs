using MediatR;
using decorativeplant_be.Application.Common.DTOs.Commerce;
using decorativeplant_be.Application.Common.DTOs.Common;

namespace decorativeplant_be.Application.Features.Commerce.Returns.Queries;

public class GetReturnByIdQuery : IRequest<ReturnRequestResponse?>
{
    public Guid Id { get; set; }
}

public class GetMyReturnsQuery : IRequest<PagedResult<ReturnRequestResponse>>
{
    public Guid UserId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class GetAllReturnsQuery : IRequest<PagedResult<ReturnRequestResponse>>
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
