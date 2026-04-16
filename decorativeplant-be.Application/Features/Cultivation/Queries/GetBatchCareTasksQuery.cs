using decorativeplant_be.Application.Common.DTOs.Garden;
using decorativeplant_be.Application.Features.Cultivation.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Queries;

public class GetBatchCareTasksQuery : IRequest<PagedResultDto<BatchCareTaskDto>>
{
    public string? Status { get; set; }
    public string? SearchTerm { get; set; }
    public string? SortOrder { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public Guid? BranchId { get; set; }
}

