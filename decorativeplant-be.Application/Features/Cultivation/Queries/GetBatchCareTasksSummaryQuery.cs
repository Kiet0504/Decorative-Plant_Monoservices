using decorativeplant_be.Application.Features.Cultivation.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Queries;

public class GetBatchCareTasksSummaryQuery : IRequest<BatchCareTasksSummary>
{
    public Guid? BranchId { get; set; }
}
