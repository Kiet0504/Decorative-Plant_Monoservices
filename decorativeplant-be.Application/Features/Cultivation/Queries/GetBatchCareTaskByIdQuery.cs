using decorativeplant_be.Application.Features.Cultivation.DTOs;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Queries;

public class GetBatchCareTaskByIdQuery : IRequest<BatchCareTaskDetailDto?>
{
    public Guid Id { get; set; }
}
