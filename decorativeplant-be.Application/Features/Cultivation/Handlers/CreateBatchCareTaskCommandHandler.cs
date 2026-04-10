using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Cultivation.Commands;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MediatR;

namespace decorativeplant_be.Application.Features.Cultivation.Handlers;

public class CreateBatchCareTaskCommandHandler : IRequestHandler<CreateBatchCareTaskCommand, Guid>
{
    private readonly IApplicationDbContext _context;

    public CreateBatchCareTaskCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateBatchCareTaskCommand request, CancellationToken cancellationToken)
    {
        // Find batch by code if possible
        var batch = await _context.PlantBatches
            .FirstOrDefaultAsync(b => b.BatchCode == request.Batch, cancellationToken);

        var details = new Dictionary<string, string>
        {
            { "product_name", request.ProductName },
            { "due_date", request.Date },
            { "frequency", request.Frequency },
            { "repeat_every", request.RepeatEvery },
            { "care_requirement", request.CareRequirement },
            { "status", "Pending" }
        };

        var log = new CultivationLog
        {
            Id = Guid.NewGuid(),
            BatchId = batch?.Id,
            ActivityType = request.Activity,
            Description = request.Description,
            Details = CultivationMapper.BuildJson(details),
            PerformedAt = null, // Indicates a pending task
            PerformedBy = null
        };

        _context.CultivationLogs.Add(log);
        await _context.SaveChangesAsync(cancellationToken);

        return log.Id;
    }
}
