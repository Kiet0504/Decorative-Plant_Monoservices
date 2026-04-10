using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Cultivation.DTOs;
using decorativeplant_be.Application.Features.Cultivation.Queries;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Handlers;

public class GetBatchCareTaskByIdQueryHandler : IRequestHandler<GetBatchCareTaskByIdQuery, BatchCareTaskDetailDto?>
{
    private readonly IApplicationDbContext _context;

    public GetBatchCareTaskByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<BatchCareTaskDetailDto?> Handle(GetBatchCareTaskByIdQuery request, CancellationToken cancellationToken)
    {
        var log = await _context.CultivationLogs
            .Include(c => c.Batch)
                .ThenInclude(b => b!.Taxonomy)
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (log == null) return null;

        return CultivationMapper.ToTaskDetailDto(log);
    }
}
