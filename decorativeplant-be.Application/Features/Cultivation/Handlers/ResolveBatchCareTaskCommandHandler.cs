using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Cultivation.Commands;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MediatR;
using System.Text.Json;

namespace decorativeplant_be.Application.Features.Cultivation.Handlers;

public class ResolveBatchCareTaskCommandHandler : IRequestHandler<ResolveBatchCareTaskCommand, bool>
{
    private readonly IApplicationDbContext _context;

    public ResolveBatchCareTaskCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> Handle(ResolveBatchCareTaskCommand request, CancellationToken cancellationToken)
    {
        var log = await _context.CultivationLogs
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);

        if (log == null) return false;

        // 1. Mark as completed
        log.PerformedAt = DateTime.UtcNow;
        log.PerformedBy = request.PerformedBy;

        // 2. Update Status in JSONB Details
        var detailsDict = new Dictionary<string, string>();
        if (log.Details != null)
        {
            try
            {
                var currentDetails = JsonSerializer.Deserialize<Dictionary<string, string>>(log.Details.RootElement.GetRawText());
                if (currentDetails != null) detailsDict = currentDetails;
            }
            catch { /* Ignore parse errors and use empty dict */ }
        }

        detailsDict["status"] = "Done";
        log.Details = CultivationMapper.BuildJson(detailsDict);

        // 3. Handle Recurrence: If recurring, create the next instance
        if (detailsDict.ContainsKey("repeat_every") && detailsDict["repeat_every"] != "None")
        {
            var repeatEvery = detailsDict["repeat_every"];
            if (DateTime.TryParse(detailsDict["due_date"], out var currentDueDate))
            {
                var nextDueDate = repeatEvery switch
                {
                    "Daily" => currentDueDate.AddDays(1),
                    "Weekly" => currentDueDate.AddDays(7),
                    "Biweekly" => currentDueDate.AddDays(14),
                    "Monthly" => currentDueDate.AddMonths(1),
                    _ => currentDueDate
                };

                var nextDetails = new Dictionary<string, string>(detailsDict)
                {
                    ["status"] = "Pending",
                    ["due_date"] = nextDueDate.ToString("yyyy-MM-ddTHH:mm:ss"),
                };
                // Remove last_notified_at for the new task
                nextDetails.Remove("last_notified_at");

                var nextLog = new CultivationLog
                {
                    Id = Guid.NewGuid(),
                    BatchId = log.BatchId,
                    LocationId = log.LocationId,
                    ActivityType = log.ActivityType,
                    Description = log.Description,
                    Details = CultivationMapper.BuildJson(nextDetails),
                    PerformedAt = null,
                    PerformedBy = null
                };

                _context.CultivationLogs.Add(nextLog);
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
