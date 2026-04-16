using decorativeplant_be.Application.Common.Interfaces;
using decorativeplant_be.Application.Features.Cultivation.Commands;
using decorativeplant_be.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using MediatR;
using decorativeplant_be.Application.Services;
using decorativeplant_be.Application.Common.DTOs.Email;

namespace decorativeplant_be.Application.Features.Cultivation.Handlers;

public class CreateBatchCareTaskCommandHandler : IRequestHandler<CreateBatchCareTaskCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IEmailService _emailService;

    public CreateBatchCareTaskCommandHandler(IApplicationDbContext context, IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
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

        // Notify cultivation staff via email (Filtered by Branch)
        try
        {
            var branchId = batch?.BranchId;
            if (branchId.HasValue)
            {
                var staffEmails = await _context.UserAccounts
                    .Where(u => u.Role == "cultivation_staff" && u.IsActive)
                    .Where(u => _context.StaffAssignments.Any(sa => sa.StaffId == u.Id && sa.BranchId == branchId))
                    .Select(u => u.Email)
                    .ToListAsync(cancellationToken);

                foreach (var email in staffEmails)
                {
                    await _emailService.SendAsync(new EmailMessage
                    {
                        To = email,
                        Subject = $"[New Care Task] {request.Activity} for {request.ProductName}",
                        BodyHtml = $@"
                            <h3>New Care Task Scheduled</h3>
                            <p>A new care task has been created for your branch:</p>
                            <ul>
                                <li><b>Activity:</b> {request.Activity}</li>
                                <li><b>Plant:</b> {request.ProductName}</li>
                                <li><b>Batch:</b> {request.Batch}</li>
                                <li><b>Scheduled Date:</b> {request.Date}</li>
                                <li><b>Repeat:</b> {request.RepeatEvery}</li>
                            </ul>
                            <p>Please check your Daily Care dashboard for more details.</p>"
                    }, cancellationToken);
                }
            }
        }
        catch (Exception)
        {
            // Log error but don't fail the command if email sending fails
        }

        return log.Id;
    }
}
